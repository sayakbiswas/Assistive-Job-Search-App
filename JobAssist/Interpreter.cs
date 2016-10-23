using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobAssist
{
    public class Interpreter
    {




        public string Interpret(string utterance)
        {
            var client = new RestClient("https://api.projectoxford.ai/luis/v1/application?id=4600ad20-e123-4cbd-b46c-fb653858afc8&subscription-key=ce202902c79544d79e964d60bf055410");
            var request = new RestRequest(Method.GET);
            request.AddParameter("q", utterance);

            IRestResponse response = client.Execute(request);


            JObject responseData = JObject.Parse(response.Content);

            string intent = (string)responseData["intents"][0]["intent"];

            return intent;
        }
    }
}
