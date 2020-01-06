using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UserAPI
{
    public class InsertActionFilterAttribute : FunctionInvocationFilterAttribute
    {
        public string action;
        public string moduleName;

        CosmosHelper<dynamic> cosmos;
        Object reqObject = new object();

        UserService _service;
        UserDbContext _userDbContext;

        public InsertActionFilterAttribute()
        {
            var configuration = new ConfigurationBuilder()
             .SetBasePath(Environment.CurrentDirectory)
             .AddJsonFile("local.settings.json", true, true)
             .AddEnvironmentVariables()
             .Build();
            var options = new DbContextOptionsBuilder<UserDbContext>().UseSqlServer(configuration["Values:SqlConnectionString"]);
            _userDbContext = new UserDbContext(options.Options);

            _service = new UserService(_userDbContext);
            cosmos = new CosmosHelper<dynamic>();
        }

        public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            string jsonObj = string.Empty;

            var req = (DefaultHttpRequest)executingContext.Arguments.FirstOrDefault().Value;

            string bodyOrQueryString = req.Body.CanSeek ? "Body" : "Query";

            switch (bodyOrQueryString)
            {
                case "Body":
                    jsonObj = ReadFromBody(req);
                    break;

                case "Query":
                    jsonObj = ReadFromQueryString(req);
                    break;

                default:
                    return base.OnExecutingAsync(executingContext, cancellationToken);
            }

            if (string.IsNullOrEmpty(jsonObj))
                return base.OnExecutingAsync(executingContext, cancellationToken);

            reqObject = JsonConvert.DeserializeObject(jsonObj);
            return base.OnExecutingAsync(executingContext, cancellationToken);
        }

        private string ReadFromBody(DefaultHttpRequest req)
        {
            var copyBuffer = new MemoryStream();
            req.Body.CopyToAsync(copyBuffer);

            //if there was no stream 
            if (copyBuffer == Stream.Null)
                return string.Empty;

            copyBuffer.Position = 0;

            using (StreamReader stream = new StreamReader(copyBuffer))
            {
                return stream.ReadToEndAsync().Result;
            }
        }

        private string ReadFromQueryString(DefaultHttpRequest req)
        {
            try
            {
                var keys = req.Query.Keys;
                Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();

                foreach (var key in keys)
                {
                    keyValuePairs.Add(key, req.Query[key]);
                }

                return DictionaryToJson(keyValuePairs);
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public string DictionaryToJson(Dictionary<string, object> dict)
        {
            var entries = dict.Select(d =>
                string.Format("\"{0}\": \"{1}\"", d.Key, string.Join(",", d.Value)));
            return "{" + string.Join(",", entries) + "}";
        }

        public override Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            // add to event db only if sql update succeeds
            //if (!executedContext.FunctionResult.Succeeded)
            try
            {

                var user = JsonConvert.DeserializeObject<User>(JsonConvert.SerializeObject(reqObject));

                //if (!_service.ValidateUser(user))
                //    return base.OnExecutedAsync(executedContext, cancellationToken);

                var cosmosresponse = cosmos.AppendToCosmos<dynamic>(action, moduleName, reqObject);

                return base.OnExecutedAsync(executedContext, cancellationToken);
            }
            catch(Exception ex)
            {
                return null;
            }
        }
    }
}
