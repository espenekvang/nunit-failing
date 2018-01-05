using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Owin.Security.Authorization;

namespace NUnit.Failing.Controllers
{
    [Route]
    [MyAuthorization]
    public class SimpleController : ApiController
    {
        [HttpGet]
        [Route]
        public async Task<IHttpActionResult> Get()
        {
            return await Task.FromResult(Ok("Hello world"));
        }
    }

    public class MyAuthorizationAttribute : AuthorizeAttribute, IAuthorizeData
    {
        public string Policy { get; set; }
        public string ActiveAuthenticationSchemes { get; set; }
    }
}