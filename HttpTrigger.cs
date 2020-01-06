using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;
using System.Threading;
using Newtonsoft.Json;
using System.IO;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace UserAPI
{
    public class HttpTrigger
    {
        private readonly UserDbContext _context;
        private CosmosHelper<dynamic> _cosmosHelper;

        public HttpTrigger(UserDbContext context, CosmosHelper<dynamic> cosmosHelper)
        {
            _context = context;
            _cosmosHelper = cosmosHelper;
        }

        [FunctionName("GetUsers")]
        public IActionResult GetUsers(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP GET/users trigger function processed a request.");

            var usersArray = _context.users.OrderBy(p => p.UserName).ToArray();
            return new OkObjectResult(usersArray);
        }

        [FunctionName("GetUserDetails")]
        public IActionResult GetUserDetails(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/details")] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP GET/user details trigger function processed a request.");

                var userId = req.Query["id"];

                var user = _context.users.Where(x => x.UserId == Guid.Parse(userId)).FirstOrDefault();
                return new OkObjectResult(user);
            }
            catch (Exception ex)
            {
                return new BadRequestResult();
            }
        }

        [FunctionName("InsertUser")]
        [InsertActionFilter(action = "Insert", moduleName = "User")]
        public IActionResult InsertUser(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "users/insert")] HttpRequest req,
            ILogger log)
        {
            try
            {
                req.Body.Position = 0;

                log.LogInformation("C# HTTP POST/users trigger function processed a request.");

                var content = new StreamReader(req.Body).ReadToEndAsync().Result;

                User user = JsonConvert.DeserializeObject<User>(content);

                UserService service = new UserService(_context);

                if (service.ValidateUser(user))
                {
                    _context.users.Add(user);
                    _context.SaveChanges();
                }
                else
                {
                    return new BadRequestObjectResult(false);
                }

                return new OkObjectResult(user.UserId);
            }
            catch (Exception ex)
            {
                return new BadRequestResult();
            }
        }

        [FunctionName("UpdateUser")]
        [InsertActionFilter(action = "Update", moduleName = "User")]
        public IActionResult UpdateUser(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "users/update")] HttpRequest req,
            ILogger log)
        {
            try
            {
                req.Body.Position = 0;

                log.LogInformation("C# HTTP PUT/users trigger function processed a request.");

                var content = new StreamReader(req.Body).ReadToEndAsync().Result;

                var user = JsonConvert.DeserializeObject<User>(content);

                _context.users.Update(user);
                _context.SaveChanges();

                return new OkObjectResult(user.UserId);
            }
            catch (Exception ex)
            {
                return new BadRequestResult();
            }
        }

        [FunctionName("DeleteUser")]
        [InsertActionFilter(action = "Delete", moduleName = "User")]
        public IActionResult DeleteUser(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "users/delete")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP PUT/users trigger function processed a request.");

            string id = req.Query["userId"];

            User user = FindUser(id);

            _context.users.Remove(user);
            _context.SaveChanges();

            return new OkObjectResult(user.UserId);
        }


        [FunctionName("GetEvents")]
        public dynamic GetEvents(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "users/getEvents")] HttpRequest req,
            ILogger log)
        {
            try
            {
                DateTimeOffset time = Convert.ToDateTime(req.Query["time"].ToString()).ToUniversalTime();

                string collection = req.Query["collection"].ToString().Replace('/', ' ');

                if (string.IsNullOrEmpty(time.ToString()) || string.IsNullOrEmpty(collection))
                    return new BadRequestObjectResult("Please pass a time and collection name on the query string or in the request body");

                return _cosmosHelper.GetByLastUpdatedTime<dynamic>(req.Query["collection"], time.ToString()).Result;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private User FindUser(string userId)
        {
            return _context.users.Where(x => x.UserId == Guid.Parse(userId)).FirstOrDefault();
        }
    }
}
