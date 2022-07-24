using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;

namespace Core.Arango.DevExtreme.Sandbox.Controllers;

[ModelBinder(BinderType = typeof(DataSourceLoadOptionsBinder))]
public class DataSourceLoadOptions : DataSourceLoadOptionsBase
{
    public List<Guid?> ParentIds { get; set; } = new();
}