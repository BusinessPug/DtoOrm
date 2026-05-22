using System.Data.Common;

namespace DtoOrm.Core;

public interface IRowMapper
{
    TDto Map<TDto>(DbDataReader reader);
}
