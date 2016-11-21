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
        private SpeechRecognitionEngine _recognizer;

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

        public Interpreter intepreter = new Interpreter();

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


            //ActivateRecognitionCommand = new RelayCommand(ActivateRecognition);
            //SpeakCommand = new RelayCommand(Speak);
            StartDialogueCommand = new RelayCommand(ManageDialogue);


            //Initialize recognition engine
            InitializeRecognitionEngine();
            InitializeSynthesizer();


            // Dialogue();

            //Initialize MongoDB client
            client = new MongoClient(mongoURI);
            ajsDatabase = client.GetDatabase("assisstive-job-search");
        }

        //Disregard this  -  only used to test API without walking through speech process
        private void api_test()
        {

            var client = new RestClient("https://api.projectoxford.ai/luis/v1/application?id=4600ad20-e123-4cbd-b46c-fb653858afc8&subscription-key=ce202902c79544d79e964d60bf055410");
            var request = new RestRequest(Method.GET);
            //request.AddParameter("id", "id=4600ad20-e123-4cbd-b46c-fb653858afc8");
            //request.AddParameter("subscription-key", "ce202902c79544d79e964d60bf055410");
            request.AddParameter("q", "yes go ahead");

            IRestResponse response = client.Execute(request);


            JObject responseData = JObject.Parse(response.Content);

             string intent = (string)responseData["intents"][0]["intent"];

           // string intent = response.ToString();

            Debug.WriteLine(intent);


        }

        private void Dialogue()
        {
            PromptBuilder builder = new PromptBuilder();

            if (step == 0)
            {
                builder.StartSentence();

                builder.AppendText("I'm sorry, I did not understand what you said.");

                builder.EndSentence();

                if(!String.IsNullOrEmpty(helpText))
                {
                    builder.StartSentence();

                    builder.AppendText(helpText);

                    builder.EndSentence();
                }
                step = previousStep;

            }

            if (step == 1)
            {
                if(firstRun == 0)
                {
                    builder.AppendText("Welcome to job assist. Speak your responses after the beep. Say quit at any time to exit.");
                    firstRun++;

                }


                builder.StartSentence();

                builder.AppendText("Would you like to search for jobs today? ");

                builder.EndSentence();

                builder.AppendBookmark("1");


            }

            if (step == 2)
            {
                builder.StartSentence();

                builder.AppendText("Ok. What type of job would you like to search for?");

                builder.EndSentence();

                builder.AppendBookmark("2");
            }

            if (step == 3)
            {
                builder.StartSentence();

                string jobText = string.Format("You would like to search for {0} jobs? Is that correct?", answer);
                builder.AppendText(jobText );

                builder.EndSentence();

                builder.AppendBookmark("3");
            }

            if (step == 4)
            {
                builder.StartSentence();

                builder.AppendText("Would you like to search for jobs in a specific city or state?");

                builder.EndSentence();

                builder.AppendBookmark("4");
            }

            if (step == 5)
            {
                builder.StartSentence();

                builder.AppendText("Ok. What is the city, state or zip code that you would like to search?");

                builder.EndSentence();

                builder.AppendBookmark("5");
            }


            if (step == 6)
            {
                builder.StartSentence();

                string jobText = string.Format("Ok, {0}. Is that right?", answer);
                builder.AppendText(jobText);

                builder.EndSentence();

                builder.AppendBookmark("6");
            }

            if (step == 7)
            {
                builder.StartSentence();

                builder.AppendText("I will now search for jobs.");

                builder.EndSentence();

                builder.AppendBookmark("7");
            }

            if (step == 8)
            {
                builder.StartSentence();

                string searchResults = string.Format("I found {0} job listings.", jobSearchResults);
                builder.AppendText(searchResults);

                builder.EndSentence();

               

                if(Convert.ToInt32(jobSearchResults) > 0)
                {

                    builder.StartSentence();

                    builder.AppendText("Would you like to review the listings?");

                    builder.EndSentence();

                }


                builder.AppendBookmark("8");
            }


            if (step == 9) //reviewing the job listings 
            {
                
                int jobNumber = 1;
           

                foreach (Job j in jobs)
                {
                    //speak job title
                    

                    string jobTitle = String.Format("Job number {0}, {1}", jobNumber, j.jobtitle);
                    _synthesizer.Speak(jobTitle);

                    

                    //speak snippet/description
                    

                    string jobDescription = String.Format("Job description {0}", j.snippet);
                    _synthesizer.Speak(jobDescription);


                    _synthesizer.Speak("Would you like to save this job?");
   
                    Console.Beep();
                    //_recognizer.Recognize();
                    this.micClient.StartMicAndRecognition();

                    answer = intepreter.Interpret(answer);

                    if (answer == "Yes" || answer == "yes")
                    {
                        string saveFile = @"C:\Users\Julian\Desktop\job_assist_" + DateTime.Now.Date.ToString("MMM-dd-yyyy") + ".txt";

                        string jobInformation = String.Format("Job title: {0}. Job description: {1}", j.jobtitle, j.snippet);

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

                        _synthesizer.Speak("The job has been saved.");
                    }

                    Thread.Sleep(1500);

                    if(answer != "quit" && answer != "Quit")
                    {
                        _synthesizer.Speak("Would you like to get salary information for this job?");

                        Console.Beep();
                        //_recognizer.Recognize();
                        this.micClient.StartMicAndRecognition();

                        answer = intepreter.Interpret(answer);

                        if (answer == "Yes" || answer == "yes")
                        {
                            //Call Glassdoor API to get salary information

                            var client = new RestClient("http://www.glassdoor.com/api/json/search/jobProgression.htm");
                            var request = new RestRequest(Method.GET);
                            request.AddParameter("t.p", "102234");
                            request.AddParameter("t.k", "egSVvV0B2Jg");
                            request.AddParameter("format", "json");
                            request.AddParameter("v", "1");
                            // request.AddParameter("q", "software developer");
                            request.AddParameter("jobTitle", jobType);
                            request.AddParameter("countryId", "1");

                            IRestResponse response = client.Execute(request);


                            JObject jobsData = JObject.Parse(response.Content);

                            string medianSalary = (string)jobsData["response"]["payMedian"];

                            // Debug.WriteLine(medianSalary);

                            string salaryInfo = string.Format("The median salary for {0} jobs is {1} dollars.", jobType, medianSalary);
                            _synthesizer.Speak(salaryInfo);


                        }

                        Thread.Sleep(1500);
                    }


                    if (answer != "quit" && answer != "Quit")
                    {

                        _synthesizer.Speak("Would you like to hear the next job or begin a new search?");

                        Console.Beep();
                        //_recognizer.Recognize();
                        this.micClient.StartMicAndRecognition();

                        answer = intepreter.Interpret(answer);

                    }

                    if (answer == "No" || answer == "no" || answer == "NewSearch" || answer == "new search" || answer == "begin a new search")
                    {
                        step = 2;
                        Dialogue();
                    }
                    else if(answer == "quit" || answer == "Quit")
                    {
                        step = 100;
                        Dialogue();
                    }


                    jobNumber++;

                }

            }


            if (step == 100)
            {
                _synthesizer.Speak("Ok. You would like to quit?");

                Console.Beep();
                //_recognizer.Recognize();
                this.micClient.StartMicAndRecognition();

                answer = intepreter.Interpret(answer);

                if (answer == "Yes" || answer == "yes" )
                {
                    builder.StartSentence();

                    builder.AppendText("Thank you for using Job Assist. Goodbye.");

                    builder.EndSentence();

                    _synthesizer.Speak(builder);

                    getAllUtterancesFromDB();

                    Environment.Exit(0);

                    //Application.Current.Shutdown();
                    //Process.GetCurrentProcess().Kill();




                }
                else
                {
                    step = 2;
                    Dialogue();

                }




            }

            _synthesizer.Speak(builder);


        }

        private void InitializeSynthesizer()
        {
            _synthesizer = new SpeechSynthesizer();

            _synthesizer.SetOutputToDefaultAudioDevice();

            //_synthesizer.BookmarkReached += BookmarkReached;
        }

        private void BookmarkReached(object sender, BookmarkReachedEventArgs e)
        {
            Debug.WriteLine(e.Bookmark);

            if(e.Bookmark != "7" && !(e.Bookmark == "8" && Convert.ToInt32(jobSearchResults) == 0 ))
            {
                Console.Beep();
                //_recognizer.Recognize();

                //Bing Speech Recognition
                this.micClient.StartMicAndRecognition();
            }

            if(e.Bookmark == "2") //job search query
            {
                jobType = string.Empty;
                jobType = answer;

            }

            if (e.Bookmark == "5") //job search query
            {
                jobLocation = string.Empty;
                jobLocation = answer;


            }

            Thread.Sleep(3000);

            if (e.Bookmark == "1")
            {
                Debug.WriteLine("answer :: " + answer);
                //Insert user respose to DB
                BsonDocument step1UtteranceDocument = new BsonDocument {
                    {"utterance", answer }
                };
                var userUtterancesStep1 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step1");
                userUtterancesStep1.InsertOneAsync(step1UtteranceDocument);

                answer = intepreter.Interpret(answer);

                Debug.WriteLine("Would you like to search for jobs today: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 2;

                }
                else if (answer == "No" || answer == "no")
                {
                    step = 100;
                }
                else if (answer == "Quit" || answer == "quit")
                {
                    step = 100;
                }
                else
                {
                    step = 0;
                    helpText = "Try saying yes or no.";
                }
    
            }


            if (e.Bookmark == "2")
            {
                //Insert user utterance to DB
                BsonDocument step2UtteranceDocument = new BsonDocument {
                    {"utterance", answer }
                };
                var userUtterancesStep2 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step2");
                userUtterancesStep2.InsertOneAsync(step2UtteranceDocument);

                Debug.WriteLine("What type of job would you like to search for: " + answer);
                if(answer == "quit")
                {
                    step = 100;

                }
                else
                {
                    step = 3;
                }
                

            }


            if (e.Bookmark == "3")
            {
                //Insert user utterance to DB
                BsonDocument step3UtteranceDocument = new BsonDocument {
                    {"utterance", answer }
                };
                var userUtterancesStep3 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step3");
                userUtterancesStep3.InsertOneAsync(step3UtteranceDocument);

                answer = intepreter.Interpret(answer);

                Debug.WriteLine("You would like to search for [job type] jobs?: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 4;

                }
                else if (answer == "No" || answer == "no")
                {
                    step = 2;
                }
                else if (answer == "Quit" || answer == "quit")
                {
                    step = 100;
                }
                else
                {
                    step = 0;
                    previousStep = 2;
                    helpText = "Try saying yes or no.";
                }

            }

            if (e.Bookmark == "4")
            {
                //Insert user utterance to DB
                BsonDocument step4UtteranceDocument = new BsonDocument {
                    {"utterance", answer }
                };
                var userUtterancesStep4 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step4");
                userUtterancesStep4.InsertOneAsync(step4UtteranceDocument);

                answer = intepreter.Interpret(answer);

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
                else if (answer == "Quit" || answer == "quit")
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

            if (e.Bookmark == "5")
            {
                //Insert user utterance to DB
                BsonDocument step5UtteranceDocument = new BsonDocument {
                    {"utterance", answer }
                };
                var userUtterancesStep5 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step5");
                userUtterancesStep5.InsertOneAsync(step5UtteranceDocument);

                Debug.WriteLine("You would like to search for jobs in: " + answer);
                step = 6;

            }

            if (e.Bookmark == "6")
            {
                //Insert user utterance to DB
                BsonDocument step6UtteranceDocument = new BsonDocument {
                    {"utterance", answer }
                };
                var userUtterancesStep6 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step6");
                userUtterancesStep6.InsertOneAsync(step6UtteranceDocument);

                answer = intepreter.Interpret(answer);

                Debug.WriteLine("You would like to search for jobs in [place]: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 7;

                }
                else if (answer == "No" || answer == "no")
                {
                    step = 5;
                }
                else if (answer == "Quit" || answer == "quit")
                {
                    step = 100;
                }
                else
                {
                    step = 0;
                    previousStep = 5;
                    helpText = "Try saying yes or no.";
                }

            }


            if (e.Bookmark == "7")
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


            if (e.Bookmark == "8")
            {
                if(Convert.ToInt32(jobSearchResults) == 0)
                {
                    step = 2;
                }
                else
                {
                    //Insert user utterance to DB
                    BsonDocument step8UtteranceDocument = new BsonDocument {
                        {"utterance", answer }
                    };
                    var userUtterancesStep8 = ajsDatabase.GetCollection<BsonDocument>("user-utterances-step8");
                    userUtterancesStep8.InsertOneAsync(step8UtteranceDocument);

                    Debug.WriteLine("Would you like to review the listings? " + answer);
                    answer = intepreter.Interpret(answer);

                    if (answer == "Yes" || answer == "yes")
                    {
                        step = 9;

                    }
                    else if (answer == "No" || answer == "no")
                    {
                        step = 7;
                    }
                    else if (answer == "Quit" || answer == "quit")
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

            Dialogue();

        }

        private void SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            _synthesizer.SpeakAsyncCancelAll();
            Debug.WriteLine("Stopping all speech");
            if(step == 3)
            {
                Debug.WriteLine("Speak Completed");
                Console.Beep();
                _recognizer.RecognizeAsync();
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
            if (_recognizer != null)
                _recognizer.Dispose();

            if (_synthesizer != null)
                _synthesizer.Dispose();
        }

        private void InitializeRecognitionEngine()
        {
            /*_recognizer = new SpeechRecognitionEngine();

            _recognizer.SetInputToDefaultAudioDevice();

            _recognizer.LoadGrammar(new DictationGrammar());

            //Cap response time to 4 seconds
            _recognizer.BabbleTimeout = TimeSpan.FromSeconds(4);


            //Set to timeout if nothing is said in 3 seconds
            _recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(3);

            _recognizer.RecognizeCompleted += RecognizeCompleted;

            _recognizer.SpeechRecognized += SpeechRecognized;*/

            //Create Microphone Reco client - Bing Speech
            this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                SpeechRecognitionMode.ShortPhrase,
                "en-US",
                this.SubscriptionKey);
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnResponseReceived += this.OnMicResponseReceivedHandler;
        }

        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e) //this is needed for synchronous listening
        {

            
            if (e.Result != null)
            {
                answer = e.Result.Text;
                Debug.WriteLine("Listening");
                Debug.WriteLine(answer);
            }
            else
            {
                answer = "I have no idea what you just said";
            }
        }

        private void RecognizeCompleted(object sender, RecognizeCompletedEventArgs e) //this is needed for asynchronous listening
        {
            if (e.Result != null)
            {
                answer = e.Result.Text;
                Debug.WriteLine("Listening");
                Debug.WriteLine(answer);
            }
            else
            {
                answer = "I have no idea what you just said";
            }

        }


        private void ActivateRecognition()
        {
             RecognizedText = string.Empty;
            _recognizer.RecognizeAsync();
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
            Debug.WriteLine("Inside RecognizeResult");
            if(e.PhraseResponse.Results.Length == 0)
            {
                Debug.WriteLine("No Response");
            }
            else
            {
                for(int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    Debug.WriteLine(e.PhraseResponse.Results[i].Confidence + " " + e.PhraseResponse.Results[i].DisplayText);
                    answer = e.PhraseResponse.Results[i].DisplayText;
                }
                if(!isMicRecording && answer != null && answer.Length != 0)
                {

                }
            }
        }

        //Dialogue Manager - Bing Speech
        private void ManageDialogue()
        {
            //TODO: Put conditions in this section to wait for user response.
            while(true)
            {
                if(!isMicRecording && systemTurn)
                {
                    PromptBuilder builder = new PromptBuilder();

                    Debug.WriteLine("Build Sentence to Speak ...");
                    builder = BuildDialogue(builder);
                    Debug.WriteLine("Build Sentence to Speak ... Done");

                    Debug.WriteLine("Speak Sentence ...");
                    _synthesizer.Speak(builder);
                    Debug.WriteLine("Speak Sentence ... Done");

                    if (step == 100 && shouldSayQuitMessage)
                    {
                        getAllUtterancesFromDB();
                        Debug.WriteLine("Quitting ...");
                        Environment.Exit(0);
                    }

                    Debug.WriteLine("Beeping ...");
                    Console.Beep();

                    systemTurn = false;

                    Debug.WriteLine("Start Speech Recognition ...");
                    this.micClient.StartMicAndRecognition();
                }

                if(!isMicRecording && userTurn)
                {
                    Debug.WriteLine("Speech Recognition ... Done");

                    Debug.WriteLine("Save user utterance to DB ...");
                    SaveUserUtteranceToDB();
                    Debug.WriteLine("Save user utterance to DB ... Done");

                    if(step == 1 || step == 3 || step == 4 || step == 6 || step == 8)
                    {
                        Debug.WriteLine("Interpret user speech ...");
                        answer = intepreter.Interpret(answer);
                        Debug.WriteLine("Interpret user speech ... Done");
                    }

                    Debug.WriteLine("Handle user intent ...");
                    HandleIntent();
                    Debug.WriteLine("Handle user intent ... Done");
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
                builder.AppendText("Would you like to search for jobs today? ");
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
                if((!shouldGetSalaryInfo && !shouldSaveJob && !shouldSpeakJobSalary) || shouldSpeakNextJob)
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
        private void HandleIntent()
        {
            if(step == 1)
            {
                Debug.WriteLine("Would you like to search for jobs today: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 2;

                }
                else if (answer == "No" || answer == "no")
                {
                    step = 100;
                }
                else if (answer == "Quit" || answer == "quit")
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
                Debug.WriteLine("What type of job would you like to search for: " + answer);
                if (answer == "quit")
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
                Debug.WriteLine("You would like to search for [job type] jobs?: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 4;

                }
                else if (answer == "No" || answer == "no")
                {
                    step = 2;
                }
                else if (answer == "Quit" || answer == "quit")
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
                else if (answer == "Quit" || answer == "quit")
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
                Debug.WriteLine("You would like to search for jobs in: " + answer);
                jobLocation = answer;
                lastLocation = answer;
                step = 6;
            }
            else if(step == 6)
            {
                Debug.WriteLine("You would like to search for jobs in [place]: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 7;

                }
                else if (answer == "No" || answer == "no")
                {
                    step = 5;
                }
                else if (answer == "Quit" || answer == "quit")
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
                    Debug.WriteLine("Would you like to review the listings? " + answer);
                    if (answer == "Yes" || answer == "yes")
                    {
                        step = 9;
                    }
                    else if (answer == "No" || answer == "no")
                    {
                        step = 7;
                    }
                    else if (answer == "Quit" || answer == "quit")
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
                if(!shouldGetSalaryInfo && !shouldSaveJob && !shouldSpeakJobSalary)
                {
                    Debug.WriteLine("Would you like to save this job? " + answer);
                    if (answer == "Yes" || answer == "yes")
                    {
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
                        shouldSaveJob = true;
                    }
                }

                if (shouldGetSalaryInfo && !shouldSpeakJobSalary)
                {
                    if (answer == "Yes" || answer == "yes")
                    {
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
                }

                if(askForNextJobOrNewSearch)
                {
                    if (answer == "No" || answer == "no" || answer == "NewSearch" || answer == "new search" 
                        || answer == "begin a new search")
                    {
                        step = 2;
                    }
                    else if (answer == "quit" || answer == "Quit")
                    {
                        step = 100;
                    } else
                    {
                        shouldSpeakNextJob = true;
                    }
                    askForNextJobOrNewSearch = false;
                }
            }
            else if(step == 100)
            {
                if (answer == "Yes" || answer == "yes")
                {
                    shouldSayQuitMessage = true;
                }
                else
                {
                    step = 2;
                }
            }
        }
    }
}
