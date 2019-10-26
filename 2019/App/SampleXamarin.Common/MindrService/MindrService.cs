using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SampleXamarin.MindrService
{
    public class MindrService
    {
        public MindrService()
        {
        }

        public const string BaseUrl = "http://23.101.64.67";

        public async Task<MindrCreateResult> CreateAnchorAsync(string name, string anchorId)
        {
            try
            {
                var htc = new HttpClient();
                var result = await htc.PostAsync($"{BaseUrl}/api/create", new StringContent(JsonConvert.SerializeObject(new MindrCreateAnchor
                {
                    name = name,
                    anchorId = Convert.ToBase64String(Encoding.UTF8.GetBytes(anchorId))
                }), Encoding.UTF8, "application/json"));
                var rc = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<MindrCreateResult>(rc);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<MindrGetAnchorContents> TryGetContentsForAnchor(string anchorId)
        {
            try
            {
                var htc = new HttpClient();
                var result = await htc.GetStringAsync($"{BaseUrl}/api/content/{Encoding.UTF8.GetBytes(anchorId)}");
                var obj = JsonConvert.DeserializeObject<MindrGetAnchorContents>(result);
                return obj;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
