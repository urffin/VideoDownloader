using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace VideoDownloader
{
    public class Program
    {
        public static JsonSerializerSettings settings = new JsonSerializerSettings() { ContractResolver = new DefaultContractResolver() { NamingStrategy = new CamelCaseNamingStrategy() } };
        public static async Task Main(string[] args)
        {

            var cookie = new System.Net.CookieContainer();
            using (var handler = new HttpClientHandler() { CookieContainer = cookie })
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.149 Safari/537.36");
                client.BaseAddress = new Uri($"{args[1]}/index.php");

                cookie.Add(new Uri(args[1]), new System.Net.Cookie("id", args[2]));

                foreach (var data in GetData())
                {
                    foreach (var key in data.Keys)
                    {
                        Console.WriteLine($"{DateTime.Now}: {data.Dir} - '{key}'. Start.");
                        await LoadVideo(client, key, data.Dir);
                        Console.WriteLine($"{DateTime.Now}: {data.Dir} - '{key}'. End.");
                        await Task.Delay(10000);
                    }
                }
            }
        }
        private static Data[] GetData()
        {
            return JsonConvert.DeserializeObject<Data[]>("[{\"dir\":\"dir1\",\"keys\":[\"15\",\"16\"]},{\"dir\":\"dir2\",\"keys\":[\"19\",\"20\",\"21\",\"26\",\"27\"]},{\"dir\":\"dir3\",\"keys\":[\"28\",\"29\",\"30\",\"31\",\"32\",\"33\",\"208\",\"354\"]},{\"dir\":\"dir4\",\"keys\":[\"484\",\"485\",\"486\",\"487\",\"488\",\"489\",\"490\"]},{\"dir\":\"dir5\",\"keys\":[\"36\",\"37\"]}]");
        }
        private static async Task LoadVideo(HttpClient client, string videoKey, string dir)
        {
            MultipartFormDataContent mfd = CreateFormData("init", new JsonData(videoKey));
            Console.WriteLine($"{ DateTime.Now}: send init request");
            var initResponse = await client.PostAsync("", mfd);

            var contentRange = initResponse.Content.Headers.ContentRange;

            Console.WriteLine($"{ DateTime.Now}: start read stream init request");
            var initStream = await initResponse.Content.ReadAsStreamAsync();
            Console.WriteLine($"{ DateTime.Now}: end read stream init request");
            var path = Path.Combine(dir, initResponse.Content.Headers.ContentDisposition.FileName.Trim('"'));
            Console.WriteLine($"{ DateTime.Now}: prepare for {path}");
            EnsureFolder(path);
            Console.WriteLine($"{DateTime.Now}: Start load {dir} - '{videoKey}'");
            using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                await initStream.CopyToAsync(fs);
                var i = 0;
                while (contentRange.Length - contentRange.To > 1)
                {
                    mfd = CreateFormData("by_time", new JsonDataByTime(videoKey, i));
                    var response = await client.PostAsync("", mfd);
                    contentRange = response.Content.Headers.ContentRange;
                    var stream = await response.Content.ReadAsStreamAsync();
                    fs.Seek((long)contentRange.From, SeekOrigin.Begin);
                    Console.Write(".");
                    i += 64;
                    await stream.CopyToAsync(fs);
                    await Task.Delay(2000);
                }
                Console.WriteLine("");
            }
            Console.WriteLine($"{DateTime.Now}: Loaded {dir} - '{videoKey}'");
        }

        private static void EnsureFolder(string path)
        {
            var dir = Path.GetDirectoryName(path);
            Console.Write($"{DateTime.Now}: check dir '{dir}' -");
            if (Directory.Exists(dir))
            {
                Console.WriteLine($" exist");
                return;
            }

            Console.WriteLine($" not exist");
            Console.WriteLine($"{DateTime.Now}: create '{dir}'");
            Directory.CreateDirectory(dir);

        }

        private static MultipartFormDataContent CreateFormData(string request, object data)
        {
            var mfd = new MultipartFormDataContent
            {
                { new StringContent("chunk"), "handler" },
                { new StringContent(request), "request" },
                { new StringContent(JsonConvert.SerializeObject(data, settings)), "data" },
                { new StringContent("secure_streaming_v2_gateway"), "ajax_id" },
                { new StringContent("true"), "ajax" }
            };
            return mfd;
        }
        class JsonData
        {
            public string Key { get; set; }
            public string Extra { get; } = "b_h9W9G";

            public JsonData(string key)
            {
                Key = key;
            }
        }
        class JsonDataByTime : JsonData
        {
            public bool Seeking { get; } = false;
            public int Multiplier { get; } = 32;
            public int Position;
            [JsonProperty("back_load")]
            public bool BackLoad { get; } = false;
            public JsonDataByTime(string key, int position) : base(key)
            {
                Position = position;
            }
        }

        class Data
        {
            public string Dir { get; set; }
            public string[] Keys { get; set; }
        }
    }
}
