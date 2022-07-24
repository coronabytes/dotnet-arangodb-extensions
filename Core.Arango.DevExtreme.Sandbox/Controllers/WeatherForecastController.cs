using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.Mvc;

namespace Core.Arango.DevExtreme.Sandbox.Controllers
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
    }

    [ApiController]
    [Route("api/grid")]
    public class DevExController : ControllerBase
    {
        private static readonly List<Project> _projects = new()
        {
            new Project
            {
                Id = 1,
                Name = "Alpha",
                Category = "A"
            },
            new Project
            {
                Id = 2,
                Name = "Beta",
                Category = "B"
            },
            new Project
            {
                Id = 3,
                Name = "Gamma",
                Category = "B"
            }
        };

        [HttpGet("linq")]
        public LoadResult Linq(DataSourceLoadOptions loadOptions)
        {
            return DataSourceLoader.Load(_projects.AsQueryable(), loadOptions);
        }

        [HttpGet("arango")]
        public LoadResult Arango(DataSourceLoadOptions loadOptions)
        {
            var at = new ArangoTransform(loadOptions, new ArangoTransformSettings
            {

            });

            at.Transform(out var error);

            return DataSourceLoader.Load(_projects.AsQueryable(), loadOptions);
        }
    }
}