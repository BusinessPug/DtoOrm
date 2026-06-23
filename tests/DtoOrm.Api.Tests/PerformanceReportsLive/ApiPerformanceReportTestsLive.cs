using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security;
using System.Text;
using DtoOrm.Api.Tests.DepartmentEndpointTestsLive;
using Xunit;
using Xunit.Abstractions;

namespace DtoOrm.Api.Tests.PerformanceReportsLive;

[Collection(LiveApiTestCollection.Name)]
public sealed class ApiPerformanceReportTestsLive
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private readonly LiveApiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ApiPerformanceReportTestsLive(LiveApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Api_speed_report_can_be_exported_as_jmeter_style_docx()
    {
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        var scenarios = new[]
        {
            new PerformanceScenario("Oevelse 1 - Departments 10 brugere", "/api/departments/", 10, 1),
            new PerformanceScenario("Oevelse 1 - Departments 50 brugere", "/api/departments/", 50, 1),
            new PerformanceScenario("Oevelse 1 - Departments 100 brugere", "/api/departments/", 100, 1),
            new PerformanceScenario("Liste", "/api/departments/", 50, 2),
            new PerformanceScenario("Detalje", "/api/departments/1", 50, 2),
            new PerformanceScenario("Database rapport", "/api/reports/offering-seats", 50, 2),
            new PerformanceScenario("Fuld kursusliste", "/api/courses?take=100&skip=0", 50, 2),
            new PerformanceScenario("Paginering 10 kurser", "/api/courses?take=10&skip=0", 50, 2),
            new PerformanceScenario("Studerende liste", "/api/students?take=50&skip=0&isActive=true", 50, 2),
        };

        var results = new List<PerformanceResult>(scenarios.Length);
        foreach (var scenario in scenarios)
        {
            await WarmUpAsync(client, scenario.Endpoint);
            var result = await RunScenarioAsync(client, scenario);
            results.Add(result);
            _output.WriteLine(result.ToSummaryLine());
        }

        var report = PerformanceExerciseReport.FromResults(
            DateTimeOffset.Now,
            _fixture.ApiBaseUri,
            results);

        var artifactDirectory = GetArtifactDirectory();
        Directory.CreateDirectory(artifactDirectory);

        var docxPath = Path.Combine(artifactDirectory, "api-performance-report.docx");
        var csvPath = Path.Combine(artifactDirectory, "api-performance-results.csv");
        PerformanceDocxWriter.Write(docxPath, report);
        PerformanceCsvWriter.Write(csvPath, report.Results);

        _output.WriteLine($"DOCX: {docxPath}");
        _output.WriteLine($"CSV:  {csvPath}");
        _output.WriteLine("");
        _output.WriteLine(PerformanceMarkdownWriter.ToMarkdownTable(report.Results));

        Assert.All(results, result => Assert.Equal(0, result.FailedRequests));
        Assert.True(File.Exists(docxPath), $"Expected DOCX report at {docxPath}.");
        Assert.True(File.Exists(csvPath), $"Expected CSV report at {csvPath}.");
    }

    private static async Task WarmUpAsync(HttpClient client, string endpoint)
    {
        using var response = await client.GetAsync(endpoint).ConfigureAwait(false);
        await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    private static async Task<PerformanceResult> RunScenarioAsync(HttpClient client, PerformanceScenario scenario)
    {
        var samples = new List<RequestSample>(scenario.Requests);
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wallClock = new Stopwatch();
        var sampleLock = new object();

        var users = Enumerable.Range(0, scenario.Users)
            .Select(_ => Task.Run(async () =>
            {
                await startSignal.Task.ConfigureAwait(false);

                for (var loop = 0; loop < scenario.LoopCount; loop++)
                {
                    var sample = await MeasureRequestAsync(client, scenario.Endpoint).ConfigureAwait(false);
                    lock (sampleLock)
                    {
                        samples.Add(sample);
                    }
                }
            }))
            .ToArray();

        wallClock.Start();
        startSignal.SetResult();
        await Task.WhenAll(users).ConfigureAwait(false);
        wallClock.Stop();

        return PerformanceResult.FromSamples(scenario, samples, wallClock.Elapsed);
    }

    private static async Task<RequestSample> MeasureRequestAsync(HttpClient client, string endpoint)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(endpoint).ConfigureAwait(false);
            await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            stopwatch.Stop();

            return new RequestSample(stopwatch.Elapsed, response.StatusCode, null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new RequestSample(stopwatch.Elapsed, null, ex.GetType().Name);
        }
    }

    private static string GetArtifactDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "start-all.ps1")))
            {
                return Path.Combine(directory.FullName, "artifacts", "performance");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "artifacts", "performance");
    }

    private sealed record PerformanceScenario(string Name, string Endpoint, int Users, int LoopCount)
    {
        public int Requests => Users * LoopCount;
    }

    private sealed record RequestSample(TimeSpan Elapsed, HttpStatusCode? StatusCode, string? Error)
    {
        public bool Failed => Error is not null || StatusCode is null || (int)StatusCode >= 400;
    }

    private sealed record PerformanceResult(
        string Name,
        string Endpoint,
        int Users,
        int LoopCount,
        int Requests,
        double AverageMs,
        double MaxMs,
        double ThroughputPerSecond,
        double ErrorPercent,
        int FailedRequests,
        TimeSpan WallClock)
    {
        public static PerformanceResult FromSamples(
            PerformanceScenario scenario,
            IReadOnlyCollection<RequestSample> samples,
            TimeSpan wallClock)
        {
            var requestCount = samples.Count;
            var failed = samples.Count(sample => sample.Failed);
            var averageMs = requestCount == 0 ? 0 : samples.Average(sample => sample.Elapsed.TotalMilliseconds);
            var maxMs = requestCount == 0 ? 0 : samples.Max(sample => sample.Elapsed.TotalMilliseconds);
            var throughput = wallClock.TotalSeconds <= 0 ? 0 : requestCount / wallClock.TotalSeconds;
            var errorPercent = requestCount == 0 ? 0 : failed * 100.0 / requestCount;

            return new PerformanceResult(
                scenario.Name,
                scenario.Endpoint,
                scenario.Users,
                scenario.LoopCount,
                requestCount,
                averageMs,
                maxMs,
                throughput,
                errorPercent,
                failed,
                wallClock);
        }

        public string ToSummaryLine()
            => string.Join(
                " | ",
                Name,
                Endpoint,
                $"brugere={Users}",
                $"loop={LoopCount}",
                $"requests={Requests}",
                $"avg={AverageMs.ToString("N2", Danish)} ms",
                $"max={MaxMs.ToString("N2", Danish)} ms",
                $"throughput={ThroughputPerSecond.ToString("N2", Danish)}/s",
                $"errors={ErrorPercent.ToString("N2", Danish)}%");
    }

    private sealed record PerformanceExerciseReport(
        DateTimeOffset GeneratedAt,
        Uri ApiBaseUri,
        IReadOnlyList<PerformanceResult> Results,
        IReadOnlyList<ExerciseSection> Exercises)
    {
        public static PerformanceExerciseReport FromResults(
            DateTimeOffset generatedAt,
            Uri apiBaseUri,
            IReadOnlyList<PerformanceResult> results)
        {
            var exercise1 = results
                .Where(result => result.Name.StartsWith("Oevelse 1", StringComparison.Ordinal))
                .ToArray();
            var list = Find(results, "Liste");
            var detail = Find(results, "Detalje");
            var databaseReport = Find(results, "Database rapport");
            var fullCourses = Find(results, "Fuld kursusliste");
            var pagedCourses = Find(results, "Paginering 10 kurser");
            var studentList = Find(results, "Studerende liste");
            var slowest = results.MaxBy(result => result.AverageMs) ?? results[0];
            var highestError = results.MaxBy(result => result.ErrorPercent) ?? results[0];

            var sections = new[]
            {
                new ExerciseSection(
                    "Oevelse 1 - Foerste belastningstest",
                    "Endpointet /api/departments/ blev koert med 10, 50 og 100 samtidige brugere og loop count 1.",
                    RowsFor(exercise1),
                    new[]
                    {
                        TrendText(exercise1),
                        ErrorText(exercise1),
                        $"Mest overraskende resultat: {BestByThroughput(exercise1).Name} gav den hoejeste throughput med {FormatNumber(BestByThroughput(exercise1).ThroughputPerSecond)}/s."
                    }),
                new ExerciseSection(
                    "Oevelse 2 - Find en flaskehals",
                    "Endpointet /api/reports/offering-seats henter data via flere joins og group by og er derfor et godt sted at lede efter database-flaskehalse.",
                    RowsFor(databaseReport),
                    new[]
                    {
                        $"Mulig flaskehals: databaseforespoergslen bag {databaseReport.Endpoint}.",
                        $"Den blev fundet ved at sammenligne gennemsnitlig responstid; {databaseReport.Name} maalte {FormatNumber(databaseReport.AverageMs)} ms i gennemsnit.",
                        highestError.ErrorPercent > 0
                            ? $"Symptom: {highestError.Name} havde {FormatNumber(highestError.ErrorPercent)}% fejl."
                            : "Symptom: Der opstod ingen HTTP-fejl i testen; hold stadig oeje med API-loggen for connection pool warnings under hoejere belastning."
                    }),
                new ExerciseSection(
                    "Oevelse 3 - Sammenlign to endpoints",
                    "Der sammenlignes en listeforespoergsel med en detaljeforespoergsel paa departments.",
                    RowsFor(list, detail),
                    new[]
                    {
                        FasterEndpointText(list, detail),
                        "Detalje-endpointet forventes normalt at vaere hurtigere, fordi det returnerer en enkelt raekke.",
                        "Liste-endpointet sender mest data, fordi det returnerer alle departments."
                    }),
                new ExerciseSection(
                    "Oevelse 4 - Pagination",
                    "Projektet bruger take/skip i stedet for /paged, saa testen sammenligner /api/courses?take=100&skip=0 med /api/courses?take=10&skip=0.",
                    RowsFor(fullCourses, pagedCourses),
                    new[]
                    {
                        $"Forskellen i gennemsnitlig responstid var {FormatNumber(Math.Abs(fullCourses.AverageMs - pagedCourses.AverageMs))} ms.",
                        "Fordelen ved pagination er, at API'et sender faerre objekter og databasen kan stoppe tidligere.",
                        "Systemer med lister, soegning, ordrelinjer, historik og dashboards har typisk gavn af pagination."
                    }),
                new ExerciseSection(
                    "Oevelse 5 - Optimeringsforslag",
                    $"Det svaereste endpoint i denne koorsel var {slowest.Endpoint}.",
                    RowsFor(slowest),
                    new[]
                    {
                        $"Problemet er hoejere responstid: {slowest.Name} maalte {FormatNumber(slowest.AverageMs)} ms i gennemsnit og {FormatNumber(slowest.MaxMs)} ms max.",
                        "Problemet blev fundet ved at sortere resultaterne efter Average ms.",
                        "Flaskehalsen er sandsynligvis databasearbejde eller for stor response, afhaengigt af endpointet.",
                        SuggestOptimization(slowest)
                    }),
                new ExerciseSection(
                    "Oevelse 6 - Performanceanalyse af eget projekt",
                    "Analysen tager udgangspunkt i school API'et i samples/DtoOrm.Api.",
                    RowsFor(studentList, fullCourses, databaseReport),
                    new[]
                    {
                        "Data kommer fra MariaDB-tabeller som students, courses, departments, course_offerings og enrollments.",
                        "Funktionen kan vokse fra faa seed-data objekter til mange tusinde elever, kurser og tilmeldinger.",
                        "Databasekald udfoeres gennem DtoOrm med SELECT, WHERE, LIMIT/OFFSET, JOIN, GROUP BY og COUNT.",
                        "Langsomme dele ved datavaekst: store lister uden stram pagination, joins over enrollments og rapporter med group by.",
                        "Forventet flaskehals: databaseforespoergsler og connection pool under mange samtidige brugere.",
                        "Mindst een optimering: behold pagination som standard, returner kun felter UI'et bruger, og cache rapport-endpoints der ikke aendrer sig ofte."
                    })
            };

            return new PerformanceExerciseReport(generatedAt, apiBaseUri, results, sections);
        }

        private static PerformanceResult Find(IReadOnlyList<PerformanceResult> results, string name)
            => results.First(result => result.Name == name);

        private static PerformanceResult BestByThroughput(IReadOnlyCollection<PerformanceResult> results)
            => results.MaxBy(result => result.ThroughputPerSecond) ?? results.First();

        private static IReadOnlyList<PerformanceResult> RowsFor(params PerformanceResult[] results)
            => results;

        private static string TrendText(IReadOnlyCollection<PerformanceResult> results)
        {
            var first = results.OrderBy(result => result.Users).First();
            var last = results.OrderBy(result => result.Users).Last();
            var direction = last.AverageMs > first.AverageMs ? "langsommere" : "ikke langsommere";
            return $"Systemet blev {direction}: average gik fra {FormatNumber(first.AverageMs)} ms ved {first.Users} brugere til {FormatNumber(last.AverageMs)} ms ved {last.Users} brugere.";
        }

        private static string ErrorText(IReadOnlyCollection<PerformanceResult> results)
        {
            var maxError = results.Max(result => result.ErrorPercent);
            return maxError > 0
                ? $"Der opstod fejl i testen: hoejeste fejlprocent var {FormatNumber(maxError)}%."
                : "Der opstod ingen fejl i denne koorsel.";
        }

        private static string FasterEndpointText(PerformanceResult left, PerformanceResult right)
        {
            var faster = left.AverageMs <= right.AverageMs ? left : right;
            return $"{faster.Endpoint} var hurtigst med {FormatNumber(faster.AverageMs)} ms i gennemsnit.";
        }

        private static string SuggestOptimization(PerformanceResult result)
        {
            if (result.Endpoint.Contains("take=100", StringComparison.OrdinalIgnoreCase) ||
                result.Endpoint.Contains("students", StringComparison.OrdinalIgnoreCase))
            {
                return "Valgt loesning: brug mindre page size og returner faerre felter i listevisninger.";
            }

            if (result.Endpoint.Contains("reports", StringComparison.OrdinalIgnoreCase))
            {
                return "Valgt loesning: indeks paa join/filter-kolonner og caching af rapport-resultater.";
            }

            return "Valgt loesning: maal SQL'en, tilfoej pagination hvor relevant, og cache stabile laese-endpoints.";
        }

        private static string FormatNumber(double value)
            => value.ToString("N2", Danish);
    }

    private sealed record ExerciseSection(
        string Title,
        string Description,
        IReadOnlyList<PerformanceResult> Rows,
        IReadOnlyList<string> Reflection);

    private static class PerformanceCsvWriter
    {
        public static void Write(string path, IReadOnlyList<PerformanceResult> results)
        {
            var lines = new List<string>
            {
                "Navn;Endpoint;Brugere;Loop;Requests;Average ms;Max ms;Throughput/s;Error %"
            };

            lines.AddRange(results.Select(result => string.Join(
                ";",
                Csv(result.Name),
                Csv(result.Endpoint),
                result.Users.ToString(Danish),
                result.LoopCount.ToString(Danish),
                result.Requests.ToString(Danish),
                result.AverageMs.ToString("N2", Danish),
                result.MaxMs.ToString("N2", Danish),
                result.ThroughputPerSecond.ToString("N2", Danish),
                result.ErrorPercent.ToString("N2", Danish))));

            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        private static string Csv(string value)
            => value.Contains(';') || value.Contains('"') || value.Contains('\n')
                ? '"' + value.Replace("\"", "\"\"") + '"'
                : value;
    }

    private static class PerformanceMarkdownWriter
    {
        public static string ToMarkdownTable(IReadOnlyList<PerformanceResult> results)
        {
            var builder = new StringBuilder();
            builder.AppendLine("| Navn | Endpoint | Brugere | Loop | Requests | Average ms | Max ms | Throughput/s | Error % |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var result in results)
            {
                builder.AppendLine(string.Join(
                    " | ",
                    "| " + result.Name,
                    result.Endpoint,
                    result.Users.ToString(Danish),
                    result.LoopCount.ToString(Danish),
                    result.Requests.ToString(Danish),
                    result.AverageMs.ToString("N2", Danish),
                    result.MaxMs.ToString("N2", Danish),
                    result.ThroughputPerSecond.ToString("N2", Danish),
                    result.ErrorPercent.ToString("N2", Danish) + " |"));
            }

            return builder.ToString();
        }
    }

    private static class PerformanceDocxWriter
    {
        public static void Write(string path, PerformanceExerciseReport report)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", PackageRelationshipsXml);
            WriteEntry(archive, "word/document.xml", BuildDocumentXml(report));
        }

        private static string BuildDocumentXml(PerformanceExerciseReport report)
        {
            var body = new StringBuilder();
            body.Append(Paragraph("API Performance test", "Title"));
            body.Append(Paragraph($"Genereret: {report.GeneratedAt.LocalDateTime.ToString("dd-MM-yyyy HH:mm:ss", Danish)}"));
            body.Append(Paragraph($"Base URL: {report.ApiBaseUri}"));
            body.Append(Paragraph("Samlet JMeter-lignende resultattabel", "Heading1"));
            body.Append(ResultsTable(report.Results));

            foreach (var exercise in report.Exercises)
            {
                body.Append(Paragraph(exercise.Title, "Heading1"));
                body.Append(Paragraph(exercise.Description));
                body.Append(ResultsTable(exercise.Rows));
                body.Append(Paragraph("Refleksion", "Heading2"));

                foreach (var reflection in exercise.Reflection)
                {
                    body.Append(Bullet(reflection));
                }
            }

            body.Append(SectionProperties());

            return $$"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                    {{body}}
                  </w:body>
                </w:document>
                """;
        }

        private static string ResultsTable(IReadOnlyList<PerformanceResult> results)
        {
            var rows = new StringBuilder();
            rows.Append(TableRow("Navn", "Endpoint", "Brugere", "Loop", "Requests", "Average ms", "Max ms", "Throughput/s", "Error %"));

            foreach (var result in results)
            {
                rows.Append(TableRow(
                    result.Name,
                    result.Endpoint,
                    result.Users.ToString(Danish),
                    result.LoopCount.ToString(Danish),
                    result.Requests.ToString(Danish),
                    result.AverageMs.ToString("N2", Danish),
                    result.MaxMs.ToString("N2", Danish),
                    result.ThroughputPerSecond.ToString("N2", Danish),
                    result.ErrorPercent.ToString("N2", Danish)));
            }

            return $$"""
                <w:tbl>
                  <w:tblPr>
                    <w:tblStyle w:val="TableGrid"/>
                    <w:tblW w:w="0" w:type="auto"/>
                    <w:tblBorders>
                      <w:top w:val="single" w:sz="4" w:space="0" w:color="808080"/>
                      <w:left w:val="single" w:sz="4" w:space="0" w:color="808080"/>
                      <w:bottom w:val="single" w:sz="4" w:space="0" w:color="808080"/>
                      <w:right w:val="single" w:sz="4" w:space="0" w:color="808080"/>
                      <w:insideH w:val="single" w:sz="4" w:space="0" w:color="808080"/>
                      <w:insideV w:val="single" w:sz="4" w:space="0" w:color="808080"/>
                    </w:tblBorders>
                  </w:tblPr>
                  {{rows}}
                </w:tbl>
                """;
        }

        private static string TableRow(params string[] cells)
        {
            var builder = new StringBuilder("<w:tr>");
            foreach (var cell in cells)
            {
                builder.Append("<w:tc><w:tcPr><w:tcW w:w=\"1800\" w:type=\"dxa\"/></w:tcPr>");
                builder.Append(Paragraph(cell));
                builder.Append("</w:tc>");
            }

            builder.Append("</w:tr>");
            return builder.ToString();
        }

        private static string Paragraph(string text, string? style = null)
        {
            var styleXml = style is null
                ? string.Empty
                : $"<w:pPr><w:pStyle w:val=\"{Xml(style)}\"/></w:pPr>";
            return $"<w:p>{styleXml}<w:r><w:t xml:space=\"preserve\">{Xml(text)}</w:t></w:r></w:p>";
        }

        private static string Bullet(string text)
            => $"<w:p><w:pPr><w:ind w:left=\"360\" w:hanging=\"180\"/></w:pPr><w:r><w:t>- {Xml(text)}</w:t></w:r></w:p>";

        private static string SectionProperties()
            => """
                <w:sectPr>
                  <w:pgSz w:w="16838" w:h="11906" w:orient="landscape"/>
                  <w:pgMar w:top="720" w:right="720" w:bottom="720" w:left="720" w:header="450" w:footer="450" w:gutter="0"/>
                </w:sectPr>
                """;

        private static void WriteEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string Xml(string value)
            => SecurityElement.Escape(value) ?? string.Empty;

        private const string ContentTypesXml = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
            </Types>
            """;

        private const string PackageRelationshipsXml = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
            </Relationships>
            """;
    }
}
