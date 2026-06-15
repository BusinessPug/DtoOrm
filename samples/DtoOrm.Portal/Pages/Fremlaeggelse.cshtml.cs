using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DtoOrm.Portal.Pages;

/// <summary>
/// The "secret" full-screen presentation deck served at <c>/fremlaeggelse</c>.
/// It is intentionally not linked from the site navigation.
///
/// All code listings shown on the slides live here as plain C# strings so the
/// Razor view never has to parse generics (<c>&lt;T&gt;</c>) or parameter tokens
/// (<c>@p0</c>); the view simply emits them and Razor HTML-encodes them safely.
/// </summary>
public sealed class FremlaeggelseModel : PageModel
{
    public void OnGet()
    {
    }

    // ORM: the typed, fluent, composable query (real Reports handler)
    public string TypedBuildSnippet => """
        var e = Db.Tables.Enrollments;   // typede tabeller - genereret ud fra skemaet
        var o = Db.Tables.Offerings;
        var c = Db.Tables.Courses;

        var enrollments = Aggregates.Count(e.Id, "EnrollmentCount");

        var popular = await session
            .From(e)
            .InnerJoin(o, o.Id.EqColumn(e.OfferingId))
            .InnerJoin(c, c.Id.EqColumn(o.CourseId))
            .Select(c.Id.As("CourseId"), c.Code, c.Title, enrollments)
            .GroupBy(c.Id, c.Code, c.Title)
            .Having(enrollments.Gte(5))
            .OrderByDescending(enrollments)
            .ToListAsync<CoursePopularityDto>(ct);
        """;

    // The SQL that the builder above renders
    public string GeneratedSqlSnippet => """
        SELECT `c`.`id`    AS `CourseId`,
               `c`.`code`  AS `Code`,
               `c`.`title` AS `Title`,
               COUNT(`e`.`id`) AS `EnrollmentCount`
        FROM `enrollments` AS `e`
        INNER JOIN `offerings` AS `o` ON `o`.`id` = `e`.`offering_id`
        INNER JOIN `courses`   AS `c` ON `c`.`id` = `o`.`course_id`
        GROUP BY `c`.`id`, `c`.`code`, `c`.`title`
        HAVING COUNT(`e`.`id`) >= @p0          -- ← værdien er en bunden parameter
        ORDER BY COUNT(`e`.`id`) DESC
        """;

    // Typed primitives: Column<T>
    public string ColumnSnippet => """
        public sealed class Column<T> : IColumn
        {
            public Type ClrType => typeof(T);

            public string Render(ISqlDialect d)
                => $"{d.QuoteIdentifier(Table.Alias)}.{d.QuoteIdentifier(DbName)}";
        }

        // Fordi det er typet, fanger compileren fejl FØR vi rammer databasen:
        session.From(students)
               .Select(students.Id, students.Email)
               .Where(students.IsActive.Eq(true));   // bool - ikke "true" som tekst
        """;

    // Security: every value becomes a bound parameter
    public string ParamSnippet => """
        public string AddParameter(object? value)
        {
            var name = Dialect.GetParameterName(_parameters.Count);  // @p0, @p1, @p2 ...
            _parameters.Add(new SqlParameterValue(name, value));
            return name;                                             // brugerinput rører
        }                                                            // ALDRIG SQL-teksten

        column.Eq("' OR 1=1 --")   →   `s`.`email` = @p0     (uskadeligt - bundet værdi)
        column.Eq(null)            →   `s`.`email` IS NULL   (null håndteres korrekt)
        """;

    // Sessions & transactions
    public string TxSnippet => """
        await using var session = MariaDbOrm.Create(connectionString);

        await session.WithTransactionAsync(async tx =>
        {
            var offeringId = await tx.InsertInto(offerings)
                .Value(offerings.CourseId, 12)
                .Value(offerings.Capacity, 30)
                .ExecuteAndReturnIdAsync();          // SELECT LAST_INSERT_ID()

            await tx.InsertInto(enrollments)
                .Value(enrollments.OfferingId, (int)offeringId)
                .Value(enrollments.StudentId, 7)
                .ExecuteAsync();
        });   // commit ved succes - kaster den, rulles ALT tilbage automatisk
        """;

    // Source generator: schema.json → typed tables at compile time
    public string GeneratorSnippet => """
        // dtoorm.schema.json   (med i projektet som <AdditionalFiles>)
        {
          "dbName": "students", "clrName": "Students", "alias": "s",
          "columns": [
            { "dbName": "email", "clrName": "Email", "clrType": "string" }
          ]
        }

        //  ⇩  genereret ved compile-tid - ingen kode skrevet i hånden  ⇩
        public sealed class StudentsTable : Table
        {
            public Column<string> Email { get; }
        }
        public static class Tables { public static readonly StudentsTable Students = new(); }
        """;

    // API: vertical slice + CQRS
    public string CqrsSnippet => """
        // Hver feature er én lodret skive: Endpoint → Query → Handler → DTO
        group.MapGet("/{id:int}/details", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var dto = await d.QueryAsync(new GetOfferingDetailsQuery(id), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        public sealed class GetOfferingDetailsHandler
            : IQueryHandler<GetOfferingDetailsQuery, OfferingDetailsDto?>
        {
            public Task<OfferingDetailsDto?> HandleAsync(GetOfferingDetailsQuery q, CancellationToken ct)
                => _session.From(o)                       // ← her bruges min egen ORM
                    .InnerJoin(c, c.Id.EqColumn(o.CourseId)) /* ... */
                    .SingleOrDefaultAsync<OfferingDetailsDto>(ct);
        }
        """;

    // Portal: typed client, depends only on an interface
    public string PortalSnippet => """
        // Portalen kender KUN et interface - aldrig selve API-projektet.
        builder.Services.AddHttpClient<ISchoolApiClient, SchoolApiClient>(c =>
        {
            c.BaseAddress = new Uri(apiBaseUrl);          // fra appsettings
            c.Timeout = TimeSpan.FromSeconds(15);
        });

        // En Razor-side henter bare sine data og viser dem:
        public async Task OnGetAsync(int id)
            => Roster = await _api.GetOfferingRosterAsync(id);
        """;

    // "Ugly but works": the reflection dispatcher
    public string DispatcherSnippet => """
        // Én lille dispatcher router beskeder til den rette handler - via refleksion.
        private Task<TResult> InvokeAsync<TResult>(object msg, Type openHandler, CancellationToken ct)
        {
            var handlerType = openHandler.MakeGenericType(msg.GetType(), typeof(TResult));
            var handler     = _provider.GetService(handlerType)
                              ?? throw new InvalidOperationException($"Ingen handler for {msg.GetType().Name}.");
            var method      = handlerType.GetMethod("HandleAsync");
            return (Task<TResult>)method.Invoke(handler, new[] { msg, ct })!;  // refleksion pr. kald
        }
        """;
}
