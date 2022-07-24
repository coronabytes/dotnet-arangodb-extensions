using DevExtreme.AspNet.Data.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;

namespace Core.Arango.DevExtreme.Sandbox.Controllers;

public class DataSourceLoadOptionsBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var loadOptions = new DataSourceLoadOptions();
        DataSourceLoadOptionsParser.Parse(loadOptions,
            key =>
            {
                return bindingContext.ValueProvider.GetValue(key).FirstOrDefault();
            });
            
        var parentIDs = bindingContext.ValueProvider.GetValue("parentIds").FirstOrDefault();
        if (parentIDs != null)
            loadOptions.ParentIds = JsonConvert.DeserializeObject<List<Guid?>>(parentIDs);
            
        bindingContext.Result = ModelBindingResult.Success(loadOptions);
        return Task.CompletedTask;
    }
}