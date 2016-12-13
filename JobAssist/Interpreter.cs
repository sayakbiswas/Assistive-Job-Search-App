using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobAssist
{
    public class Interpreter
    {
        public JObject Interpret(string utterance)
        {
            Debug.Write("Interpreting user speech ... ");
            if (String.IsNullOrEmpty(utterance))
            {
                return null;
            }
            var client = new RestClient("https://api.projectoxford.ai/luis/v2.0/apps/49667a22-4f58-4422-a8fe-7d23c5b902e4?subscription-key=31e802b0448c49f9bfcdfba4fc0f040c&verbose=true");
            var request = new RestRequest(Method.GET);
            request.AddParameter("q", utterance);
            IRestResponse response = client.Execute(request);
            Debug.WriteLine("Response from LUIS :: " + response.Content);
            JObject responseData = JObject.Parse(response.Content);
            Debug.WriteLine("Done");
            return responseData;
        }

        public string getIntent(JObject responseData)
        {
            Debug.Write("Getting user intent ... ");
            string intent = "";
            if (responseData != null)
            {
                intent = (string)responseData["intents"][0]["intent"];
            }
            Debug.WriteLine("Done");
            Debug.WriteLine("User intent is " + intent);
            return intent == null ? "" : intent;
        }

        public Dictionary<string, string> getEntities(JObject responseData)
        {
            Debug.Write("Getting entities from speech ... ");
            Dictionary<string, string> entities = new Dictionary<string, string>();
            if(responseData != null)
            {
                foreach (JObject entity in responseData["entities"].Children<JObject>())
                {
                    string entityType = "";
                    string entityValue = "";
                    foreach (JProperty property in entity.Properties())
                    {
                        Debug.WriteLine("property " + property.Name + " value " + property.Value);
                        if (property.Name.Equals("type"))
                        {
                            entityType = (string)property.Value;
                        }

                        if (property.Name.Equals("entity"))
                        {
                            entityValue = (string)property.Value;
                        }
                    }
                    entities.Add(entityType, entityValue);
                }
            }
            Debug.WriteLine("Done");
            return entities;
        }

        public string getRatingMeaning(string rating)
        {
            if(Convert.ToDouble(rating) >= 0 && Convert.ToDouble(rating) < 2)
            {
                return "poor";
            }
            else if(Convert.ToDouble(rating) >= 2 && Convert.ToDouble(rating) < 3.5)
            {
                return "average";
            }
            else
            {
                return "very good";
            }
        }
    }
}
