using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using System.Windows.Input;
using System.Speech.Recognition;
using GalaSoft.MvvmLight.Command;
using System.Speech.Synthesis;
using System.Diagnostics;
using System.Threading;
using RestSharp;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.IO;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core;
using Microsoft.CognitiveServices.SpeechRecognition;
using System.Windows.Threading;

namespace JobAssist
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private SpeechSynthesizer _synthesizer;
        public ICommand ActivateRecognitionCommand { get; private set; }
        public ICommand SpeakCommand { get; private set; }
        public ICommand StartDialogueCommand { get; private set; }
        private string _recognizedText;
        private string _speechInput;
        public int step = 1; //Step of dialogue process
        public int previousStep = 1;
        public string answer;
        public string helpText;
        public string jobSearchResults;
        public string jobType;
        public string jobLocation;
        public int firstRun = 0;
        public bool searchByLocation = false;
        public List<Job> jobs = new List<Job>();
        public Interpreter interpreter = new Interpreter();
        public string lastJob;
        public string lastLocation;

        //MongoDB URI
        private String mongoURI = "mongodb://ajs:ajs@ds050189.mlab.com:50189/assisstive-job-search";
        MongoClient client;
        IMongoDatabase ajsDatabase;

        //Microphone Client - Bing Speech
        private MicrophoneRecognitionClient micClient;
        private string SubscriptionKey = "a32d46f7532040628570b3ab4e055922";
        private bool isMicRecording = false;

        private int jobNumber = 1;
        private bool shouldSaveJob = false;
        private bool shouldGetSalaryInfo = false;
        private bool shouldSpeakJobSalary = false;
        private bool askForNextJobOrNewSearch = false;
        private bool shouldSpeakNextJob = false;
        private bool shouldSayQuitMessage = false;
        private string currentJobTitle = "";
        private string currentJobDescription = "";
        private string currentJobMedianSalary = "";
        private bool systemTurn = true;
        private bool userTurn = false;
        private JObject interpretedSpeech;
        private bool noResponse = false;

        public string SpeechInput
        {
            get { return _speechInput; }
            set { Set(ref _speechInput, value); }
        }

        public string RecognizedText
        {
            get { return _recognizedText;  }
            set { Set(ref _recognizedText, value);  }
        }


        //Constructor
        public MainViewModel()
        {
            StartDialogueCommand = new RelayCommand(ManageDialogue);
            
            //Initialize recognition engine
            InitializeRecognitionEngine();
            InitializeSynthesizer();

            //Initialize MongoDB client
            client = new MongoClient(mongoURI);
            ajsDatabase = client.GetDatabase("assisstive-job-search");
        }

        private void InitializeSynthesizer()
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
        }

        private void SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            _synthesizer.SpeakAsyncCancelAll();
            Debug.WriteLine("Stopping all speech");
            if(step == 3)
            {
                Debug.WriteLine("Speak Completed");
                Console.Beep();
                step = 0;
            }

        }

        private void Speak()
        {
            if (!string.IsNullOrEmpty(SpeechInput))
                _synthesizer.SpeakAsync(SpeechInput);
        }

        public void Dispose()
        {
            if (this.micClient != null)
                this.micClient.Dispose();

            if (_synthesizer != null)
                _synthesizer.Dispose();
        }

        private void InitializeRecognitionEngine()
        {
            //Create Microphone Reco client - Bing Speech
            this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                SpeechRecognitionMode.ShortPhrase,
                "en-IN",
                this.SubscriptionKey);
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnResponseReceived += this.OnMicResponseReceivedHandler;
        }

        private void getAllUtterancesFromDB()
        {
            Debug.WriteLine("############ Step 1 Utterances ###########");
            var userUtterancesStep1 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step1");
            var step1Utterances = userUtterancesStep1.Find(_ => true).ToList();
            foreach(var utterance in  step1Utterances)
            {
                Debug.WriteLine(utterance);
            }

            Debug.WriteLine("############ Step 2 Utterances ###########");
            var userUtterancesStep2 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step2");
            var step2Utterances = userUtterancesStep2.Find(_ => true).ToList();
            foreach (var utterance in step2Utterances)
            {
                Debug.WriteLine(utterance);
            }

            Debug.WriteLine("############ Step 3 Utterances ###########");
            var userUtterancesStep3 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step3");
            var step3Utterances = userUtterancesStep3.Find(_ => true).ToList();
            foreach (var utterance in step3Utterances)
            {
                Debug.WriteLine(utterance);
            }

            Debug.WriteLine("############ Step 4 Utterances ###########");
            var userUtterancesStep4 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step4");
            var step4Utterances = userUtterancesStep4.Find(_ => true).ToList();
            foreach (var utterance in step4Utterances)
            {
                Debug.WriteLine(utterance);
            }

            Debug.WriteLine("############ Step 5 Utterances ###########");
            var userUtterancesStep5 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step5");
            var step5Utterances = userUtterancesStep5.Find(_ => true).ToList();
            foreach (var utterance in step5Utterances)
            {
                Debug.WriteLine(utterance);
            }

            Debug.WriteLine("############ Step 6 Utterances ###########");
            var userUtterancesStep6 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step6");
            var step6Utterances = userUtterancesStep6.Find(_ => true).ToList();
            foreach (var utterance in step6Utterances)
            {
                Debug.WriteLine(utterance);
            }

            Debug.WriteLine("############ Step 7 Utterances ###########");
            var userUtterancesStep7 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step7");
            var step7Utterances = userUtterancesStep7.Find(_ => true).ToList();
            foreach (var utterance in step7Utterances)
            {
                Debug.WriteLine(utterance);
            }

            Debug.WriteLine("############ Step 8 Utterances ###########");
            var userUtterancesStep8 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step8");
            var step8Utterances = userUtterancesStep8.Find(_ => true).ToList();
            foreach (var utterance in step8Utterances)
            {
                Debug.WriteLine(utterance);
            }
        }

        //Handler for Mic Status - Bing Speech
        private void OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            Debug.WriteLine("e.Recording " + e.Recording);
            isMicRecording = e.Recording;
        }

        //Handler for Mic Response - Bing Speech
        private void OnMicResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            Debug.WriteLine("Inside OnMicResponseReceivedHandler " + step);
            //this.micClient.EndMicAndRecognition();
            this.RecognizeResult(e);
            userTurn = true;
        }

        //Mic Response Results - Bing Speech
        private void RecognizeResult(SpeechResponseEventArgs e)
        {
            Debug.Write("Recognizing Speech ... ");
            if(e.PhraseResponse.Results.Length == 0)
            {
                Debug.WriteLine("No Response");
                noResponse = true;
                answer = "";
            }
            else
            {
                noResponse = false;
                for(int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    Debug.WriteLine(e.PhraseResponse.Results[i].Confidence + " " + e.PhraseResponse.Results[i].DisplayText);
                    answer = e.PhraseResponse.Results[i].DisplayText;
                }
            }
            Debug.WriteLine("Done");
        }

        //Dialogue Manager - Bing Speech
        private void ManageDialogue()
        {
            while(true)
            {
                if(!isMicRecording && systemTurn)
                {
                    PromptBuilder builder = new PromptBuilder();

                    Debug.Write("Build Sentence to Speak ... ");
                    builder = BuildDialogue(builder);
                    Debug.WriteLine("Done");

                    Debug.Write("Speak Sentence ... " + step);
                    _synthesizer.Speak(builder);
                    Debug.WriteLine("Done");

                    if (step == 100 && shouldSayQuitMessage)
                    {
                        getAllUtterancesFromDB();
                        Debug.WriteLine("Quitting ...");
                        Environment.Exit(0);
                    }

                    Debug.WriteLine("Beeping ...");
                    Console.Beep();

                    systemTurn = false;

                    Debug.Write("Start Speech Recognition ... ");
                    this.micClient.StartMicAndRecognition();
                }

                if(!isMicRecording && userTurn)
                {
                    Debug.WriteLine("Done");

                    /*if(String.IsNullOrEmpty(answer) && step != 7)
                    {
                        systemTurn = true;
                        userTurn = false;
                        continue;
                    }*/

                    Debug.Write("Save user utterance to DB ... ");
                    SaveUserUtteranceToDB();
                    Debug.WriteLine("Done");

                    //if(step != 2 && step != 5 && step != 7)
                    //{
                        interpretedSpeech = interpreter.Interpret(answer);
                    //}

                    Debug.Write("Handle user intent ... ");
                    HandleIntent(interpretedSpeech);
                    Debug.WriteLine("Done");
                    systemTurn = true;
                    userTurn = false;
                }
            }
        }

        //Dialogue Builder - Bing Speech
        private PromptBuilder BuildDialogue(PromptBuilder builder)
        {
            if(step == 0)
            {
                builder.StartSentence();
                builder.AppendText("I'm sorry, I did not understand what you said.");
                builder.EndSentence();
                if (!String.IsNullOrEmpty(helpText))
                {
                    builder.StartSentence();
                    builder.AppendText(helpText);
                    builder.EndSentence();
                }
                step = previousStep;
            }

            if (step == 1)
            {
                if (firstRun == 0)
                {
                    builder.AppendText("Welcome to job assist. Speak your responses after the beep. Say quit at any time to exit.");
                    firstRun++;
                }
                builder.StartSentence();
                builder.AppendText("Would you like to search for jobs today?");
                builder.EndSentence();
            }

            if (step == 2)
            {
                builder.StartSentence();
                builder.AppendText("Ok. What type of job would you like to search for?");
                builder.EndSentence();
            }

            if (step == 3)
            {
                builder.StartSentence();
                string jobText = string.Format("You would like to search for {0} jobs? Is that correct?", answer);
                builder.AppendText(jobText);
                builder.EndSentence();
            }

            if (step == 4)
            {
                builder.StartSentence();
                builder.AppendText("Would you like to search for jobs in a specific city or state?");
                builder.EndSentence();
            }

            if (step == 5)
            {
                builder.StartSentence();
                builder.AppendText("Ok. What is the city, state or zip code that you would like to search?");
                builder.EndSentence();
            }

            if (step == 6)
            {
                builder.StartSentence();
                string jobText = string.Format("Ok, {0}. Is that right?", answer);
                builder.AppendText(jobText);
                builder.EndSentence();
            }

            if (step == 7)
            {
                builder.StartSentence();
                builder.AppendText("I will now search for jobs.");
                builder.EndSentence();
            }

            if (step == 8)
            {
                builder.StartSentence();
                string searchResults = string.Format("I found {0} job listings.", jobSearchResults);
                builder.AppendText(searchResults);
                builder.EndSentence();

                if (Convert.ToInt32(jobSearchResults) > 0)
                {
                    builder.StartSentence();
                    builder.AppendText("Would you like to review the listings?");
                    builder.EndSentence();
                }
            }

            if (step == 9) //reviewing the job listings 
            {
                int count = 1;
                if((!shouldGetSalaryInfo && !shouldSaveJob && !shouldSpeakJobSalary && !askForNextJobOrNewSearch) 
                    || shouldSpeakNextJob)
                {
                    foreach (Job j in jobs)
                    {
                        if (count == jobNumber)
                        {
                            //speak job title
                            currentJobTitle = j.jobtitle;
                            builder.StartSentence();
                            string jobTitle = String.Format("Job number {0}, {1}", jobNumber, j.jobtitle);
                            builder.AppendText(jobTitle);
                            builder.EndSentence();

                            //speak snippet/description
                            currentJobDescription = j.snippet;
                            builder.StartSentence();
                            string jobDescription = String.Format("Job description {0}", j.snippet);
                            builder.AppendText(jobDescription);
                            builder.EndSentence();

                            builder.StartSentence();
                            builder.AppendText("Would you like to save this job?");
                            builder.EndSentence();
                            jobNumber++;
                            if(shouldSpeakNextJob)
                            {
                                shouldSpeakNextJob = false;
                            }
                            break;
                        }
                        count++;
                    }
                }

                if (shouldSaveJob)
                {
                    builder.StartSentence();
                    builder.AppendText("The job has been saved.");
                    builder.EndSentence();
                    shouldSaveJob = false;
                    shouldGetSalaryInfo = true;
                }

                if(shouldGetSalaryInfo)
                {
                    builder.StartSentence();
                    builder.AppendText("Would you like to get salary information for this job?");
                    builder.EndSentence();
                }

                if(shouldSpeakJobSalary)
                {
                    builder.StartSentence();
                    string salaryInfo = string.Format("The median salary for {0} jobs is {1} dollars.", 
                        jobType, currentJobMedianSalary);
                    builder.AppendText(salaryInfo);
                    builder.EndSentence();
                    shouldSpeakJobSalary = false;
                    askForNextJobOrNewSearch = true;
                }

                if(askForNextJobOrNewSearch)
                {
                    builder.StartSentence();
                    builder.AppendText("Would you like to hear the next job or begin a new search?");
                    builder.EndSentence();
                }
            }

            if(step == 100)
            {
                if(!shouldSayQuitMessage)
                {
                    builder.StartSentence();
                    builder.AppendText("Ok. You would like to quit?");
                    builder.EndSentence();
                }
                else
                {
                    builder.StartSentence();
                    builder.AppendText("Thank you for using Job Assist. Goodbye.");
                    builder.EndSentence();
                }
            }
            return builder;
        }

        //Save user utterance to DB - Bing Speech
        private void SaveUserUtteranceToDB()
        {
            Debug.WriteLine("Utterance to write in DB :: " + answer);
            if(!String.IsNullOrEmpty(answer))
            {
                BsonDocument utteranceDocument = new BsonDocument {
                    {"utterance", answer }
                };
                var userUtterances = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step" + step);
                userUtterances.InsertOneAsync(utteranceDocument);
            }
        }

        //Intent Handler - Bing Speech
        private void HandleIntent(JObject interpretedSpeech)
        {
            if(step == 1)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                Debug.WriteLine("Would you like to search for jobs today: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 2;
                }
                else if (answer == "No" || answer == "no")
                {
                    step = 100;
                }
                else if (answer.Contains("Quit") || answer.Contains("quit"))
                {
                    step = 100;
                }
                else
                {
                    step = 0;
                    helpText = "Try saying yes or no.";
                }
            }
            else if(step == 2)
            {
                Dictionary<string, string> entities = interpreter.getEntities(interpretedSpeech);
                string answerWithoutInterpretation = answer;
                foreach(String entityKey in entities.Keys)
                {
                    if(entityKey.Equals("JobType"))
                    {
                        Debug.WriteLine("entityKey " + entityKey);
                        if (entities.TryGetValue(entityKey, out answer))
                        {
                            Debug.WriteLine("entity " + answer);
                            break;
                        }
                    }
                }
                if (String.IsNullOrEmpty(answer))
                {
                    answer = answerWithoutInterpretation;
                }
                Debug.WriteLine("What type of job would you like to search for: " + answer);
                if(String.IsNullOrEmpty(answer) || noResponse)
                {
                    step = 0;
                    helpText = "Try to speak your responses a little bit louder.";
                }
                else if(answer.Contains("quit") || answer.Contains("Quit"))
                {
                    step = 100;
                }
                else
                {
                    jobType = answer;
                    lastJob = answer;
                    step = 3;
                }
            }
            else if(step == 3)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                Debug.WriteLine("You would like to search for [job type] jobs?: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 4;
                }
                else if (answer == "No" || answer == "no")
                {
                    step = 2;
                }
                else if (answer.Contains("Quit") || answer.Contains("quit"))
                {
                    step = 100;
                }
                else
                {
                    step = 0;
                    previousStep = 3;
                    answer = lastJob;
                    helpText = "Try saying yes or no.";
                }
            }
            else if(step == 4)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                Debug.WriteLine("Would you like to search for jobs in a specific city or state: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    searchByLocation = true;
                    step = 5;
                }
                else if (answer == "No" || answer == "no")
                {
                    step = 7;
                }
                else if (answer.Contains("Quit") || answer.Contains("quit"))
                {
                    step = 100;
                }
                else
                {
                    step = 0;
                    previousStep = 4;
                    helpText = "Try saying yes or no.";
                }
            }
            else if(step == 5)
            {
                Dictionary<string, string> entities = interpreter.getEntities(interpretedSpeech);
                Debug.WriteLine("entities length " + entities.Count);
                string answerWithoutInterpretation = answer;
                foreach(String entityKey in entities.Keys)
                {
                    if(entityKey.Equals("LocationEntity") || entityKey.Contains("geography"))
                    {
                        if(entities.TryGetValue(entityKey, out answer))
                        {
                            break;
                        }
                    }
                }
                if(String.IsNullOrEmpty(answer))
                {
                    answer = answerWithoutInterpretation;
                }
                Debug.WriteLine("You would like to search for jobs in: " + answer);
                jobLocation = answer;
                lastLocation = answer;
                step = 6;
            }
            else if(step == 6)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                Debug.WriteLine("You would like to search for jobs in [place]: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 7;

                }
                else if (answer == "No" || answer == "no")
                {
                    step = 5;
                }
                else if (answer.Contains("Quit") || answer.Contains("quit"))
                {
                    step = 100;
                }
                else
                {
                    step = 0;
                    previousStep = 6;
                    answer = lastLocation;
                    helpText = "Try saying yes or no.";
                }
            }
            else if(step == 7)
            {
                //Do the API Call here
                //NOTE: maximum number of results per query is set at default of 10
                var client = new RestClient("http://api.indeed.com/ads/apisearch");
                var request = new RestRequest(Method.GET);
                request.AddParameter("publisher", "6582450998153239");
                request.AddParameter("v", "2");
                request.AddParameter("q", jobType); //job search query string

                if (searchByLocation == true)
                    request.AddParameter("l", jobLocation);

                IRestResponse response = client.Execute(request);
                var xml = XDocument.Parse(response.Content);
                var query = from j in xml.Root.Descendants("result")
                            select new
                            {
                                jobTitle = j.Element("jobtitle").Value,
                                snippet = j.Element("snippet").Value
                            };
                jobSearchResults = Convert.ToString(query.Count());
                foreach (var o in query)
                {
                    //Debug.WriteLine("Job snippet " + o.snippet);
                    Job j = new Job() { jobtitle = o.jobTitle, snippet = o.snippet };
                    jobs.Add(j);
                }

                step = 8;
            }
            else if(step == 8)
            {
                if (Convert.ToInt32(jobSearchResults) == 0)
                {
                    step = 2;
                }
                else
                {
                    answer = interpreter.getIntent(interpretedSpeech);
                    Debug.WriteLine("Would you like to review the listings? " + answer);
                    if (answer == "Yes" || answer == "yes")
                    {
                        step = 9;
                    }
                    else if (answer == "No" || answer == "no")
                    {
                        step = 7;
                    }
                    else if (answer.Contains("Quit") || answer.Contains("quit"))
                    {
                        step = 100;
                    }
                    else
                    {
                        step = 0;
                        previousStep = 7;
                        helpText = "Try saying yes or no.";
                    }
                }
            }
            else if(step == 9)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                if (!shouldGetSalaryInfo && !shouldSaveJob && !shouldSpeakJobSalary && !askForNextJobOrNewSearch)
                {
                    Debug.WriteLine("Would you like to save this job? " + answer);
                    if (answer == "Yes" || answer == "yes")
                    {
                        Debug.Write("Saving Job ... ");
                        string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string saveFile = path + @"\job_assist_" + DateTime.Now.Date.ToString("MMM - dd - yyyy") + ".txt";
                        string jobInformation = String.Format("Job title: {0}. Job description: {1}",
                            currentJobTitle, currentJobDescription);
                        if (!File.Exists(saveFile))
                        {
                            File.WriteAllText(saveFile, jobInformation);
                        }
                        else
                        {
                            using (StreamWriter file = new StreamWriter(saveFile, true))
                            {
                                file.WriteLine(jobInformation);
                            }
                        }
                        Debug.Write("Done");
                        shouldSaveJob = true;
                    }
                    else
                    {
                        shouldSaveJob = false;
                        shouldGetSalaryInfo = true;
                    }
                }
                else if (shouldGetSalaryInfo && !shouldSpeakJobSalary)
                {
                    Debug.WriteLine("Getting salary info?");
                    if (answer == "Yes" || answer == "yes")
                    {
                        Debug.WriteLine("Yup, Getting salary info");
                        //Call Glassdoor API to get salary information
                        var client = new RestClient("http://www.glassdoor.com/api/json/search/jobProgression.htm");
                        var request = new RestRequest(Method.GET);
                        request.AddParameter("t.p", "102234");
                        request.AddParameter("t.k", "egSVvV0B2Jg");
                        request.AddParameter("format", "json");
                        request.AddParameter("v", "1");
                        request.AddParameter("jobTitle", jobType);
                        request.AddParameter("countryId", "1");

                        IRestResponse response = client.Execute(request);
                        JObject jobsData = JObject.Parse(response.Content);
                        currentJobMedianSalary = (string)jobsData["response"]["payMedian"];
                        shouldSpeakJobSalary = true;
                        shouldGetSalaryInfo = false;
                    }
                    else
                    {
                        shouldGetSalaryInfo = false;
                        shouldSpeakJobSalary = false;
                        askForNextJobOrNewSearch = true;
                    }
                }
                else if(askForNextJobOrNewSearch)
                {
                    if (answer == "No" || answer == "no" || answer == "NewSearch" || answer == "new search" 
                        || answer == "begin a new search")
                    {
                        step = 2;
                    }
                    else if (answer.Contains("quit") || answer.Contains("Quit"))
                    {
                        step = 100;
                    }
                    else
                    {
                        shouldSpeakNextJob = true;
                    }
                    askForNextJobOrNewSearch = false;
                }
            }
            else if(step == 100)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                Debug.WriteLine("Ok, You would like to quit?" + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    shouldSayQuitMessage = true;
                }
                else
                {
                    step = 2;
                }
                if (String.IsNullOrEmpty(answer) || noResponse)
                {
                    previousStep = 100;
                    step = 0;
                    helpText = "Try to speak your response a little bit louder.";
                }
            }
        }
    }
}
