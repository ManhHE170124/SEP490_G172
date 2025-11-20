//using System.Threading;
//using System.Threading.Tasks;
//using Keytietkiem.DTOs.Homepage;
//using Keytietkiem.Services.Interfaces;
//using Microsoft.AspNetCore.Mvc;

//namespace Keytietkiem.Controllers
//{
//    [ApiController]
//    [Route("api/homepage")]
//    public class HomepageController : ControllerBase
//    {
//        private readonly IHomepageService _homepageService;

//        public HomepageController(IHomepageService homepageService)
//        {
//            _homepageService = homepageService;
//        }

//        [HttpGet]
//        public async Task<ActionResult<HomepageResponseDto>> Get(CancellationToken cancellationToken)
//        {
//            var payload = await _homepageService.GetAsync(cancellationToken);
//            return Ok(payload);
//        }
//    }
//}
