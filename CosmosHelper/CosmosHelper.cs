using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace UserAPI
{

    public class CosmosHelper<T> where T : class, new()
    {
        public async Task AppendToCosmos<T>(string action, string collection, T obj) where T : class, new()
        {
            try
            {
                if (string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(action) || obj == null)
                {
                    return;
                }

                var json = JsonConvert.SerializeObject(obj);
                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                //add custom columns for event db
                dictionary.Add("Action", action);
                //dictionary.Add("updateTime", DateTime.Now);

                var jsonstring = DictionaryToJson(dictionary);
                await DocumentDBRepository<dynamic>.CreateItemAsync(jsonstring, collection);
            }
            catch (Exception ex)
            {
                return;
            }
        }

        private string GetNumbers(string modifiedDate)
        {
            char[] arr = modifiedDate.ToCharArray();

            arr = Array.FindAll<char>(arr, (c => (char.IsDigit(c))));
            return new string(arr);
        }

        public string DictionaryToJson(Dictionary<string, object> dict)
        {
            var entries = dict.Select(d =>
                string.Format("\"{0}\": \"{1}\"", d.Key, string.Join(",", d.Value)));
            return "{" + string.Join(",", entries) + "}";
        }

        public async Task<IList<dynamic>> GetByLastUpdatedTime<T>(string collectionName, string lastUpdatedTime)
        {
            return await DocumentDBRepository<dynamic>.QueryDataByLastRefreshTime(collectionName, lastUpdatedTime);
        }
    }
}
