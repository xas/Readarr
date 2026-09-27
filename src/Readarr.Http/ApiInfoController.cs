using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Readarr.Http
{
    public class ApiInfoController : Controller
    {
        [AllowAnonymous]
        [HttpGet("/api")]
        [Produces("application/json")]
        public ApiInfoResource GetApiInfo()
        {
            return new ApiInfoResource
            {
                Current = "v1"
            };
        }
    }
}
