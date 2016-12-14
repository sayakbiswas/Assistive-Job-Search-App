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
        public string answer = "";
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
        private bool shouldSpeakNextJob = true;
        private bool shouldSayQuitMessage = false;
        private string currentJobTitle = "";
        private string currentJobDescription = "";
        private string currentJobMedianSalary = "";
        private string currentJobCompany = "";
        private bool systemTurn = true;
        private bool userTurn = false;
        private JObject interpretedSpeech;
        private bool noResponse = false;
        private bool foundJobType = false;
        private bool foundJobLoc = false;
        private bool shouldGetCompanyInfo = false;
        private bool shouldSpeakCompanyInfo = false;
        private Company currentCompany;
        private bool shouldGetCompanyRatings = false;
        private bool shouldGetCompanyReviews = false;
        private bool shouldSpeakCompanyRatings = false;
        private bool shouldSpeakCompanyReviews = false;
        private bool hasSpokenCompanyRatings = false;
        private bool hasSpokenCompanyReviews = false;
        private bool shouldAskForSaveJob = false;
        private bool newSearch = false;
        private bool noCompanyInfo = false;

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

        private string _selectedLocale = "en-US";
        public string SelectedLocale
        {
            get { return _selectedLocale; }
            set { _selectedLocale = value.Replace("System.Windows.Controls.ComboBoxItem: ", ""); }
        }

        private int _selectedSpeechRate = 0;
        public int SelectedSpeechRate
        {
            get { return _selectedSpeechRate; }
            set { _selectedSpeechRate = value; }
        }


        //Constructor
        public MainViewModel()
        {
            StartDialogueCommand = new RelayCommand(ManageDialogue);
            
            //Initialize recognition and synthesizer engine
            //InitializeRecognitionEngine();
            //InitializeSynthesizer();

            //Initialize MongoDB client
            client = new MongoClient(mongoURI);
            ajsDatabase = client.GetDatabase("assisstive-job-search");

            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logFile = path + @"\job_assist_log_" + DateTime.Now.Date.ToString("MMM - dd - yyyy") + ".log";

            TextWriterTraceListener[] listeners = new TextWriterTraceListener[] {
                new TextWriterTraceListener(logFile),
                new TextWriterTraceListener(Console.Out)
            };
            Debug.Listeners.AddRange(listeners);
            Debug.AutoFlush = true;
        }

        private void InitializeSynthesizer()
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
            _synthesizer.Rate = _selectedSpeechRate;
        }

        private void SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            _synthesizer.SpeakAsyncCancelAll();
            Debug.WriteLine("Stopping all speech");
            if(step == 3)
            {
                Debug.WriteLine("Speak Completed");
                //Console.Beep();
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
            Debug.Write("Initializing Recognition Engine with locale " + _selectedLocale + " ... ");
            this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                SpeechRecognitionMode.ShortPhrase,
                _selectedLocale,
                this.SubscriptionKey);
            this.micClient.OnMicrophoneStatus += this.OnMicrophoneStatus;
            this.micClient.OnResponseReceived += this.OnMicResponseReceivedHandler;
            Debug.WriteLine("Done");
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
            //Initialize recognition and synthesizer engine
            InitializeRecognitionEngine();
            InitializeSynthesizer();
            while (true)
            {
                if(!isMicRecording && systemTurn)
                {
                    systemTurn = false;
                    PromptBuilder builder = new PromptBuilder();

                    Debug.Write("Build Sentence to Speak ... ");
                    builder = BuildDialogue(builder);
                    Debug.WriteLine("Done");

                    Debug.Write("Speak Sentence ... " + step);
                    _synthesizer.Speak(builder);
                    Debug.WriteLine("Done");

                    if (step == 100 && shouldSayQuitMessage)
                    {
                        //getAllUtterancesFromDB();
                        Debug.WriteLine("Quitting ...");
                        Environment.Exit(0);
                    }

                    if(!systemTurn)
                    {
                        Debug.WriteLine("Beeping ...");
                        Console.Beep();
                        Debug.Write("Start Speech Recognition ... ");
                        this.micClient.StartMicAndRecognition();
                    }
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

            if (step == 101)
            {
                builder.StartSentence();
                builder.AppendText("You can say quit to exit and new search to start the job search again. "
                    + "Try to speak short responses for best results.");
                builder.EndSentence();
                step = previousStep;
            }

            if (step == 1)
            {
                if (firstRun == 0)
                {
                    //builder.AppendText("Welcome to job assist. Speak your responses after the beep. Say quit at any time to exit.");
                    builder.AppendText("Welcome to job assist. Say new search at any time to start the job search again, and quit to exit. Speak your responses after the beep.");
                    firstRun++;
                }
                foundJobType = false;
                foundJobLoc = false;
                shouldSaveJob = false;
                shouldGetSalaryInfo = false;
                shouldSpeakJobSalary = false;
                askForNextJobOrNewSearch = false;
                shouldSpeakNextJob = true;
                shouldSayQuitMessage = false;
                shouldGetCompanyInfo = false;
                shouldSpeakCompanyInfo = false;
                shouldGetCompanyRatings = false;
                shouldGetCompanyReviews = false;
                shouldSpeakCompanyRatings = false;
                shouldSpeakCompanyReviews = false;
                hasSpokenCompanyRatings = false;
                hasSpokenCompanyReviews = false;
                shouldAskForSaveJob = false;
                noCompanyInfo = false;
                jobLocation = "";
                jobType = "";
                builder.StartSentence();
                if(previousStep == 8 || newSearch)
                {
                    builder.AppendText("Okay then, would you like to search for some other type of jobs?");
                    newSearch = false;
                }
                else
                {
                    builder.AppendText("Would you like to search for jobs today?");
                }
                builder.EndSentence();
            }

            if (step == 2)
            {
                foundJobType = false;
                foundJobLoc = false;
                jobLocation = "";
                jobType = "";
                builder.StartSentence();
                builder.AppendText("Ok. What type of job would you like to search for?");
                builder.EndSentence();
            }

            if (step == 3)
            {
                builder.StartSentence();
                string jobText = string.Format("You would like to search for {0} jobs? Is that correct?", answer.Replace(".", ""));
                if(foundJobType)
                {
                    if(previousStep == 1)
                    {
                        jobText = string.Format("Okay, I will look for {0} jobs. Did I get that right?", answer.Replace(".", ""));
                    }
                    else
                    {
                        jobText = string.Format("Okay, {0} jobs. Did I get that right?", answer.Replace(".", ""));
                    }
                }
                builder.AppendText(jobText);
                builder.EndSentence();
            }

            if (step == 4)
            {
                builder.StartSentence();
                builder.AppendText("Next, would you like to search for jobs in a specific city or state?");
                builder.EndSentence();
            }

            if (step == 5)
            {
                builder.StartSentence();
                builder.AppendText("Alright, what is the city, state or zip code that you would like me to search?");
                builder.EndSentence();
            }

            if (step == 6)
            {
                builder.StartSentence();
                string jobText = string.Format("Ok, {0}. Is that right?", answer);
                if(foundJobLoc)
                {
                    if(previousStep == 1)
                    {
                        jobText = string.Format("And, I will search in the {0} region. Correct?", answer);
                    }
                    else if(previousStep == 4)
                    {
                        jobText = string.Format("Okay, in the {0} area. Right?", answer);
                    }
                }
                builder.AppendText(jobText);
                builder.EndSentence();
            }

            if (step == 7)
            {
                builder.StartSentence();
                builder.AppendText("Okay! I will now search for jobs. Please hold on.");
                builder.EndSentence();

                step = 8;
                systemTurn = true;
                userTurn = false;
                return builder;
            }

            if (step == 8)
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
                                snippet = j.Element("snippet").Value,
                                company = j.Element("company").Value
                            };
                jobSearchResults = Convert.ToString(query.Count());
                foreach (var o in query)
                {
                    //Debug.WriteLine("Job snippet " + o.snippet);
                    Job j = new Job() { jobtitle = o.jobTitle, snippet = o.snippet, company = o.company };
                    jobs.Add(j);
                }

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
                else
                {
                    step = 1;
                    newSearch = true;
                    systemTurn = true;
                    userTurn = false;
                }
            }

            if (step == 9) //reviewing the job listings 
            {
                int count = 1;
                if(shouldSpeakNextJob)
                {
                    foreach (Job j in jobs)
                    {
                        if (count == jobNumber)
                        {
                            hasSpokenCompanyRatings = false;
                            hasSpokenCompanyReviews = false;
                            //speak job title
                            currentJobTitle = j.jobtitle;
                            currentJobCompany = j.company;
                            builder.StartSentence();
                            string jobTitle = String.Format("Okay. Job number {0} is {1} posted by the company {2}", 
                                jobNumber, j.jobtitle, j.company);
                            builder.AppendText(jobTitle);
                            builder.EndSentence();

                            //speak snippet/description
                            currentJobDescription = j.snippet;
                            builder.StartSentence();
                            string jobDescription = String.Format("The description of the job states {0}", j.snippet);
                            builder.AppendText(jobDescription);
                            builder.EndSentence();

                            if(shouldSpeakNextJob)
                            {
                                shouldSpeakNextJob = false;
                            }
                            //shouldAskForSaveJob = true;
                            shouldGetSalaryInfo = true;
                            break;
                        }
                        count++;
                    }
                }

                if(shouldAskForSaveJob)
                {
                    builder.StartSentence();
                    builder.AppendText("So, do you want me to save this job for you?");
                    builder.EndSentence();
                }

                if (shouldSaveJob)
                {
                    builder.StartSentence();
                    builder.AppendText("Done! I have saved the job.");
                    builder.EndSentence();
                    shouldSaveJob = false;
                    //shouldGetSalaryInfo = true;
                    askForNextJobOrNewSearch = true;
                }

                if(shouldGetSalaryInfo)
                {
                    builder.StartSentence();
                    builder.AppendText("Now, would you like to get salary information for this job?");
                    builder.EndSentence();
                }

                if(shouldSpeakJobSalary)
                {
                    builder.StartSentence();
                    string salaryInfo = string.Format("Got it! The median salary for {0} jobs is {1} dollars.", 
                        jobType, currentJobMedianSalary);
                    builder.AppendText(salaryInfo);
                    builder.EndSentence();
                    shouldSpeakJobSalary = false;
                    //askForNextJobOrNewSearch = true;
                    shouldGetCompanyInfo = true;
                }

                if(shouldGetCompanyInfo)
                {
                    builder.StartSentence();
                    string askCompanyInfo = string.Format("Would you like to know more about the company {0}?", currentJobCompany);
                    builder.AppendText(askCompanyInfo);
                    builder.EndSentence();
                    //shouldGetCompanyInfo = false;
                }

                if(shouldSpeakCompanyInfo)
                {
                    builder.StartSentence();
                    string askCompanySpecifics = string.Format("Okay, what would you like to know about the company? I can tell you about ratings and reviews.");
                    if (noCompanyInfo)
                    {
                        askCompanySpecifics = string.Format("I'm sorry! I can't seem to find any information on {0}.",
                            currentJobCompany);
                        systemTurn = true;
                        userTurn = false;
                        shouldAskForSaveJob = true;
                        shouldSpeakCompanyInfo = false;
                    }
                    
                    builder.AppendText(askCompanySpecifics);
                    builder.EndSentence();
                    //shouldSpeakCompanyInfo = false;
                }

                if(shouldGetCompanyRatings)
                {
                    builder.StartSentence();
                    string speakText = string.Format("So, ratings for {0}? Right?", currentJobCompany);
                    if (hasSpokenCompanyReviews)
                    {
                        speakText = string.Format("Would you like to know the company ratings as well?");
                    }
                    builder.AppendText(speakText);
                    builder.EndSentence();
                    //shouldGetCompanyRatings = false;
                }

                if(shouldSpeakCompanyRatings)
                {
                    builder.StartSentence();
                    string speakText = string.Format("Okay, on glassdoor {0} is rated around {1} points out of a possible 5.", 
                        currentJobCompany, currentCompany.overallRating);
                    builder.AppendText(speakText);
                    builder.EndSentence();
                    if (!string.IsNullOrEmpty(currentCompany.ratingDescription))
                    {
                        builder.StartSentence();
                        speakText = string.Format("Most people think it is a {0} company to work for.", 
                            currentCompany.ratingDescription);
                        builder.AppendText(speakText);
                        builder.EndSentence();
                    }
                    if(!string.IsNullOrEmpty(currentCompany.cultureAndValuesRating))
                    {
                        builder.StartSentence();
                        speakText = string.Format("The work culture is generally thought to be {0}.",
                            interpreter.getRatingMeaning(currentCompany.cultureAndValuesRating));
                        builder.AppendText(speakText);
                        builder.EndSentence();
                    }
                    if(!string.IsNullOrEmpty(currentCompany.workLifeBalanceRating))
                    {
                        builder.StartSentence();
                        speakText = string.Format("Working at the company allows for a {0} work life balance.", 
                            interpreter.getRatingMeaning(currentCompany.workLifeBalanceRating));
                        builder.AppendText(speakText);
                        builder.EndSentence();
                    }
                    if(!string.IsNullOrEmpty(currentCompany.recommendToFriendRating))
                    {
                        builder.StartSentence();
                        speakText = string.Format("Also, something to keep in mind would be that around {0} percent of people said that they would recommend {1} to a friend.",
                            currentCompany.recommendToFriendRating, currentJobCompany);
                        builder.AppendText(speakText);
                        builder.EndSentence();
                    }
                    shouldSpeakCompanyRatings = false;
                    hasSpokenCompanyRatings = true;
                    if(!hasSpokenCompanyReviews)
                    {
                        shouldGetCompanyReviews = true;
                    }
                    else
                    {
                        shouldAskForSaveJob = true;
                        systemTurn = true;
                        userTurn = false;
                        //askForNextJobOrNewSearch = true;
                    }
                }

                if(shouldGetCompanyReviews 
                    && (!string.IsNullOrEmpty(currentCompany.reviewPros) || !string.IsNullOrEmpty(currentCompany.reviewCons)))
                {
                    builder.StartSentence();
                    string speakText = string.Format("So, do you want to listen to a review about {0} from Glassdoor?", 
                        currentJobCompany);
                    if (!hasSpokenCompanyRatings)
                    {
                        speakText = string.Format("Okay, company review. Is that right?");
                    }
                    builder.AppendText(speakText);
                    builder.EndSentence();
                    //shouldGetCompanyReviews = false;
                }
                
                if(shouldSpeakCompanyReviews)
                {
                    builder.StartSentence();
                    string speakText = string.Format("Okay, here's what an employee who is a {0} says about {1}.",
                        currentCompany.reviewHeadline, currentJobCompany);
                    builder.AppendText(speakText);
                    builder.EndSentence();
                    if (!string.IsNullOrEmpty(currentCompany.reviewPros))
                    {
                        builder.StartSentence();
                        speakText = string.Format("Pros. {0}", currentCompany.reviewPros);
                        builder.AppendText(speakText);
                        builder.EndSentence();
                    }
                    if(!string.IsNullOrEmpty(currentCompany.reviewCons))
                    {
                        builder.StartSentence();
                        speakText = string.Format("Cons. {0}", currentCompany.reviewCons);
                        builder.AppendText(speakText);
                        builder.EndSentence();
                    }
                    hasSpokenCompanyReviews = true;
                    shouldSpeakCompanyReviews = false;
                    if(!hasSpokenCompanyRatings)
                    {
                        shouldGetCompanyRatings = true;
                        systemTurn = true;
                        userTurn = false;
                    }
                    else
                    {
                        shouldAskForSaveJob = true;
                        systemTurn = true;
                        userTurn = false;
                        //askForNextJobOrNewSearch = true;
                    }
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
                    builder.AppendText("Ok. You'd like to quit?");
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
            if (step == 1)
            {
                Dictionary<string, string> entities = interpreter.getEntities(interpretedSpeech);
                string answerWithoutInterpretation = answer;
                string jobTypeEntity = "";
                string jobLocEntity = "";
                foreach (String entityKey in entities.Keys)
                {
                    if (entityKey.Equals("JobType"))
                    {
                        Debug.WriteLine("entityKey " + entityKey);
                        if (entities.TryGetValue(entityKey, out jobTypeEntity))
                        {
                            Debug.WriteLine("entity " + jobTypeEntity);
                            continue;
                        }
                    }

                    if (entityKey.Equals("Location"))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            Debug.WriteLine("entity " + jobLocEntity);
                            continue;
                        }
                    }
                    if (entityKey.Contains("Cities") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            Debug.WriteLine("entity " + jobLocEntity);
                            continue;
                        }
                    }
                    if (entityKey.Contains("States") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            Debug.WriteLine("entity " + jobLocEntity);
                            continue;
                        }
                    }
                    if (entityKey.Equals("LocationEntity") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            Debug.WriteLine("entity " + jobLocEntity);
                            continue;
                        }
                    }
                    if (entityKey.Contains("geography") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            Debug.WriteLine("entity " + jobLocEntity);
                            continue;
                        }
                    }
                }
                if (!String.IsNullOrEmpty(jobTypeEntity))
                {
                    foundJobType = true;
                    jobType = jobTypeEntity;
                    previousStep = step;
                    lastJob = jobType;
                }

                if (!String.IsNullOrEmpty(jobLocEntity))
                {
                    foundJobLoc = true;
                    jobLocation = jobLocEntity;
                    lastLocation = jobLocation;
                }

                answer = interpreter.getIntent(interpretedSpeech);
                if (answer.Equals("None") || answer.Equals("none"))
                {
                    if (answerWithoutInterpretation.Contains("Yes") || answerWithoutInterpretation.Contains("yes"))
                    {
                        answer = "Yes";
                    }
                    else if (answerWithoutInterpretation.Contains("No") || answerWithoutInterpretation.Contains("no"))
                    {
                        answer = "No";
                    }
                }
                Debug.WriteLine("Would you like to search for jobs today: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    if (foundJobType)
                    {
                        answer = jobType;
                        step = 3;
                    }
                    else
                    {
                        step = 2;
                    }
                }
                else if (answer == "No" || answer == "no")
                {
                    step = 100;
                }
                else if (answer.Contains("Quit") || answer.Contains("quit"))
                {
                    step = 100;
                }
                else if(answer.Equals("NewSearch"))
                {
                    step = 1;
                    jobNumber = 1;
                    newSearch = true;
                }
                else if(answer.Equals("Help") || answer.Equals("help"))
                {
                    step = 101;
                }
                else if(string.IsNullOrEmpty(answer) || noResponse)
                {
                    previousStep = step;
                    step = 0;
                    helpText = "Try to speak your responses a little bit louder.";
                    noResponse = false;
                }
                else
                {
                    previousStep = step;
                    step = 0;
                    helpText = "Try saying yes or no.";
                }
            }
            else if (step == 2)
            {
                Dictionary<string, string> entities = interpreter.getEntities(interpretedSpeech);
                string answerWithoutInterpretation = answer;
                answer = "";
                foreach (String entityKey in entities.Keys)
                {
                    if (entityKey.Equals("JobType"))
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
                    answer = interpreter.getIntent(interpretedSpeech);
                }
                if ((String.IsNullOrEmpty(answer) || answer.Contains("None"))
                    && answerWithoutInterpretation.Split(new char[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries).Count() < 4)
                {
                    answer = answerWithoutInterpretation;
                }
                Debug.WriteLine("What type of job would you like to search for: " + answer);
                if (String.IsNullOrEmpty(answer))
                {
                    previousStep = step;
                    step = 0;
                    if (noResponse)
                    {
                        helpText = "Try to speak your responses a little bit louder.";
                        noResponse = false;
                    }
                    else if (answerWithoutInterpretation.Split(new char[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries).Count() >= 4)
                    {
                        helpText = "Please try to say the job type in one or two words.";
                    }
                }
                else if (answer.Contains("quit") || answer.Contains("Quit"))
                {
                    step = 100;
                }
                else if (answer.Equals("NewSearch"))
                {
                    step = 1;
                    jobNumber = 1;
                    newSearch = true;
                }
                else if (answer.Equals("Help") || answer.Equals("help"))
                {
                    step = 101;
                }
                else
                {
                    jobType = answer;
                    lastJob = answer;
                    step = 3;
                }
            }
            else if (step == 3)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                Dictionary<string, string> entities = interpreter.getEntities(interpretedSpeech);
                Debug.WriteLine("entities length " + entities.Count);
                string jobEntity = "";
                foreach (String entityKey in entities.Keys)
                {
                    if (entityKey.Equals("JobType"))
                    {
                        if (entities.TryGetValue(entityKey, out jobEntity))
                        {
                            break;
                        }
                    }
                }
                Debug.WriteLine("You would like to search for [job type] jobs?: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    if (foundJobLoc)
                    {
                        answer = jobLocation;
                        previousStep = 1;
                        step = 6;
                    }
                    else
                    {
                        step = 4;
                    }
                }
                else if (answer == "No" || answer == "no")
                {
                    //step = 2;
                    if (String.IsNullOrEmpty(jobEntity))
                    {
                        step = 2;
                    }
                    else
                    {
                        step = 3;
                        answer = jobEntity;
                        jobType = jobEntity;
                        lastJob = jobType;
                    }
                }
                else if (answer.Contains("Quit") || answer.Contains("quit"))
                {
                    step = 100;
                }
                else if(string.IsNullOrEmpty(answer) || noResponse)
                {
                    previousStep = step;
                    step = 0;
                    helpText = "Try to speak your responses a little bit louder.";
                    answer = lastJob;
                    noResponse = false;
                }
                else if (answer.Equals("NewSearch"))
                {
                    step = 1;
                    jobNumber = 1;
                    newSearch = true;
                }
                else if (answer.Equals("Help") || answer.Equals("help"))
                {
                    step = 101;
                }
                else
                {
                    if (String.IsNullOrEmpty(jobEntity))
                    {
                        step = 0;
                        previousStep = 3;
                        answer = lastJob;
                        helpText = "Try saying yes or no.";
                    }
                    else
                    {
                        step = 3;
                        answer = jobEntity;
                        jobType = jobEntity;
                        lastJob = jobEntity;
                    }
                }
            }
            else if (step == 4)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                Dictionary<string, string> entities = interpreter.getEntities(interpretedSpeech);
                string jobLocEntity = "";
                foreach (String entityKey in entities.Keys)
                {
                    if (entityKey.Equals("Location"))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("Cities") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("States") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                    if (entityKey.Equals("LocationEntity") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("geography") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                }
                Debug.WriteLine("Would you like to search for jobs in a specific city or state: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    searchByLocation = true;
                    if (String.IsNullOrEmpty(jobLocEntity))
                    {
                        step = 5;
                    }
                    else
                    {
                        previousStep = step;
                        step = 6;
                        answer = jobLocEntity;
                        jobLocation = jobLocEntity;
                        foundJobLoc = true;
                    }
                }
                else if (answer == "No" || answer == "no")
                {
                    step = 7;
                }
                else if (answer.Contains("Quit") || answer.Contains("quit"))
                {
                    step = 100;
                }
                else if (answer.Equals("NewSearch"))
                {
                    step = 1;
                    jobNumber = 1;
                    newSearch = true;
                }
                else if (answer.Equals("Help") || answer.Equals("help"))
                {
                    step = 101;
                }
                else if (string.IsNullOrEmpty(answer) || noResponse)
                {
                    previousStep = step;
                    step = 0;
                    helpText = "Try to speak your responses a little bit louder.";
                    noResponse = false;
                }
                else
                {
                    if (String.IsNullOrEmpty(jobLocEntity))
                    {
                        step = 0;
                        previousStep = 4;
                        helpText = "Try saying yes or no.";
                    }
                    else
                    {
                        searchByLocation = true;
                        foundJobLoc = true;
                        previousStep = step;
                        step = 6;
                        answer = jobLocEntity;
                        jobLocation = jobLocEntity;
                    }
                }
            }
            else if (step == 5)
            {
                Dictionary<string, string> entities = interpreter.getEntities(interpretedSpeech);
                Debug.WriteLine("entities length " + entities.Count);
                string answerWithoutInterpretation = answer;
                answer = "";
                foreach (String entityKey in entities.Keys)
                {
                    if (entityKey.Equals("Location"))
                    {
                        if (entities.TryGetValue(entityKey, out answer))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("Cities") && string.IsNullOrEmpty(answer))
                    {
                        if (entities.TryGetValue(entityKey, out answer))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("States") && string.IsNullOrEmpty(answer))
                    {
                        if (entities.TryGetValue(entityKey, out answer))
                        {
                            break;
                        }
                    }
                    if (entityKey.Equals("LocationEntity") && string.IsNullOrEmpty(answer))
                    {
                        if (entities.TryGetValue(entityKey, out answer))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("geography") && string.IsNullOrEmpty(answer))
                    {
                        if (entities.TryGetValue(entityKey, out answer))
                        {
                            break;
                        }
                    }
                }
                if (String.IsNullOrEmpty(answer))
                {
                    answer = answerWithoutInterpretation;
                }
                string interpretedAnswer = interpreter.getIntent(interpretedSpeech);
                Debug.WriteLine("You would like to search for jobs in: " + answer);
                if (interpretedAnswer.Equals("NewSearch") && answerWithoutInterpretation.Contains("new search"))
                {
                    step = 1;
                    jobNumber = 1;
                    newSearch = true;
                }
                else if (interpretedAnswer.Equals("Help") || interpretedAnswer.Equals("help"))
                {
                    step = 101;
                }
                else if (string.IsNullOrEmpty(answer) || noResponse)
                {
                    previousStep = step;
                    step = 0;
                    helpText = "Try to speak your responses a little bit louder.";
                    noResponse = false;
                }
                else if (answer.Contains("Quit") || answer.Contains("quit") || interpretedAnswer.Equals("Quit"))
                {
                    step = 100;
                }
                else
                {
                    jobLocation = answer;
                    lastLocation = answer;
                    step = 6;
                }
            }
            else if (step == 6)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                Dictionary<string, string> entities = interpreter.getEntities(interpretedSpeech);
                Debug.WriteLine("entities length " + entities.Count);
                string jobLocEntity = "";
                foreach (String entityKey in entities.Keys)
                {
                    if (entityKey.Equals("Location"))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("Cities") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("States") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                    if (entityKey.Equals("LocationEntity") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                    if (entityKey.Contains("geography") && string.IsNullOrEmpty(jobLocEntity))
                    {
                        if (entities.TryGetValue(entityKey, out jobLocEntity))
                        {
                            break;
                        }
                    }
                }
                Debug.WriteLine("You would like to search for jobs in [place]: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 7;

                }
                else if (answer == "No" || answer == "no")
                {
                    if (String.IsNullOrEmpty(jobLocEntity))
                    {
                        foundJobLoc = false;
                        step = 5;
                    }
                    else
                    {
                        previousStep = 6;
                        jobLocation = jobLocEntity;
                        answer = jobLocEntity;
                        lastLocation = jobLocEntity;
                        step = 6;
                    }
                }
                else if (answer.Contains("Quit") || answer.Contains("quit"))
                {
                    step = 100;
                }
                else if (answer.Equals("NewSearch"))
                {
                    step = 1;
                    jobNumber = 1;
                    newSearch = true;
                }
                else if (answer.Equals("Help") || answer.Equals("help"))
                {
                    step = 101;
                }
                else if (string.IsNullOrEmpty(answer) || noResponse)
                {
                    previousStep = step;
                    step = 0;
                    helpText = "Try to speak your responses a little bit louder.";
                    noResponse = false;
                    answer = lastLocation;
                }
                else
                {
                    if (String.IsNullOrEmpty(jobLocEntity))
                    {
                        step = 0;
                        previousStep = 6;
                        answer = lastLocation;
                        helpText = "Try saying yes or no.";
                    }
                    else
                    {
                        previousStep = step;
                        step = 6;
                        jobLocation = jobLocEntity;
                        answer = jobLocEntity;
                        lastLocation = jobLocEntity;
                    }
                }
            }
            else if (step == 8)
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
                        previousStep = step;
                        step = 1;
                        //askForNextJobOrNewSearch = true;
                    }
                    else if (answer.Contains("Quit") || answer.Contains("quit"))
                    {
                        step = 100;
                    }
                    else if (answer.Equals("NewSearch"))
                    {
                        step = 1;
                        jobNumber = 1;
                        newSearch = true;
                    }
                    else if (answer.Equals("Help") || answer.Equals("help"))
                    {
                        step = 101;
                    }
                    else if (string.IsNullOrEmpty(answer) || noResponse)
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try to speak your responses a little bit louder.";
                        noResponse = false;
                    }
                    else
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try saying yes or no.";
                    }
                }
            }
            else if (step == 9)
            {
                answer = interpreter.getIntent(interpretedSpeech);
                if (shouldAskForSaveJob)
                {
                    Debug.WriteLine("Would you like to save this job? " + answer);
                    if (answer == "Yes" || answer == "yes")
                    {
                        Debug.Write("Saving Job ... ");
                        string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string saveFile = path + @"\job_assist_" + DateTime.Now.Date.ToString("MMM - dd - yyyy") + ".txt";
                        string jobInformation = String.Format("Job title: {0}. Job description: {1}. Company: {2}",
                            currentJobTitle, currentJobDescription, currentJobCompany);
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
                        shouldAskForSaveJob = false;
                    }
                    else if (answer == "No" || answer == "no")
                    {
                        shouldSaveJob = false;
                        //shouldGetSalaryInfo = true;
                        shouldAskForSaveJob = false;
                        askForNextJobOrNewSearch = true;
                    }
                    else if (answer.Contains("Quit") || answer.Contains("quit"))
                    {
                        step = 100;
                    }
                    else if (string.IsNullOrEmpty(answer) || noResponse)
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try to speak your response a little bit louder.";
                        noResponse = false;
                    }
                    else if (answer.Equals("NewSearch"))
                    {
                        step = 1;
                        jobNumber = 1;
                        newSearch = true;
                    }
                    else if (answer.Equals("Help") || answer.Equals("help"))
                    {
                        step = 101;
                    }
                    else
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try saying yes or no.";
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
                    else if (answer == "No" || answer == "no")
                    {
                        shouldGetSalaryInfo = false;
                        shouldSpeakJobSalary = false;
                        //askForNextJobOrNewSearch = true;
                        shouldGetCompanyInfo = true;
                    }
                    else if (answer.Contains("Quit") || answer.Contains("quit"))
                    {
                        step = 100;
                    }
                    else if (string.IsNullOrEmpty(answer) || noResponse)
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try to speak your response a little bit louder.";
                        noResponse = false;
                    }
                    else if (answer.Equals("NewSearch"))
                    {
                        step = 1;
                        jobNumber = 1;
                        newSearch = true;
                    }
                    else if (answer.Equals("Help") || answer.Equals("help"))
                    {
                        step = 101;
                    }
                    else
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try saying yes or no.";
                    }
                }
                else if (shouldGetCompanyInfo)
                {
                    Debug.WriteLine("Would you like to know more about the company?" + answer);
                    if (answer == "Yes" || answer == "yes")
                    {
                        var client = new RestClient("http://api.glassdoor.com/api/api.htm");
                        var request = new RestRequest(Method.GET);
                        request.AddParameter("t.p", "102234");
                        request.AddParameter("t.k", "egSVvV0B2Jg");
                        request.AddParameter("format", "json");
                        request.AddParameter("v", "1");
                        request.AddParameter("action", "employers");
                        request.AddParameter("q", currentJobCompany);

                        IRestResponse response = client.Execute(request);
                        JObject companyData = JObject.Parse(response.Content);
                        int recordCount = Convert.ToInt32((string)companyData["response"]["totalRecordCount"]);
                        if(recordCount > 0)
                        {
                            currentCompany = new Company()
                            {
                                overallRating = (string)companyData["response"]["employers"][0]["overallRating"],
                                ratingDescription = (string)companyData["response"]["employers"][0]["ratingDescription"],
                                cultureAndValuesRating = (string)companyData["response"]["employers"][0]["cultureAndValuesRating"],
                                seniorLeadershipRating = (string)companyData["response"]["employers"][0]["seniorLeadershipRating"],
                                compensationAndBenefitsRating = (string)companyData["response"]["employers"][0]["compensationAndBenefitsRating"],
                                careerOpportunitiesRating = (string)companyData["response"]["employers"][0]["careerOpportunitiesRating"],
                                workLifeBalanceRating = (string)companyData["response"]["employers"][0]["workLifeBalanceRating"],
                                reviewHeadline = (string)companyData["response"]["employers"][0]["featuredReview"]["headline"],
                                reviewPros = (string)companyData["response"]["employers"][0]["featuredReview"]["pros"],
                                reviewCons = (string)companyData["response"]["employers"][0]["featuredReview"]["cons"],
                                recommendToFriendRating = (string)companyData["response"]["employers"][0]["recommendToFriendRating"]
                            };
                            noCompanyInfo = false;
                        }
                        else
                        {
                            noCompanyInfo = true;
                        }
                        shouldSpeakCompanyInfo = true;
                        shouldGetCompanyInfo = false;
                    }
                    else if (answer == "No" || answer == "no")
                    {
                        shouldSpeakCompanyInfo = false;
                        shouldGetCompanyInfo = false;
                        //askForNextJobOrNewSearch = true;
                        shouldAskForSaveJob = true;
                    }
                    else if (answer.Contains("Quit") || answer.Contains("quit"))
                    {
                        step = 100;
                    }
                    else if (string.IsNullOrEmpty(answer) || noResponse)
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try to speak your response a little bit louder.";
                        noResponse = false;
                    }
                    else if (answer.Equals("NewSearch"))
                    {
                        step = 1;
                        jobNumber = 1;
                        newSearch = true;
                    }
                    else if (answer.Equals("Help") || answer.Equals("help"))
                    {
                        step = 101;
                    }
                    else
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try saying yes or no.";
                    }
                }
                else if (shouldSpeakCompanyInfo)
                {
                    Debug.WriteLine("What would you like to know about the company? " + answer);
                    if (answer.Equals("Ratings"))
                    {
                        shouldSpeakCompanyInfo = false;
                        shouldGetCompanyRatings = true;
                    }
                    else if (answer.Equals("Reviews"))
                    {
                        shouldSpeakCompanyInfo = false;
                        shouldGetCompanyReviews = true;
                    }
                    else if (answer.Equals("Quit"))
                    {
                        step = 100;
                    }
                    else if (string.IsNullOrEmpty(answer) || noResponse)
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try to speak your response a little bit louder.";
                        noResponse = false;
                    }
                    else if (answer.Equals("NewSearch"))
                    {
                        step = 1;
                        jobNumber = 1;
                        newSearch = true;
                    }
                    else if (answer.Equals("Help") || answer.Equals("help"))
                    {
                        step = 101;
                    }
                    else
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try saying Ratings or Reviews.";
                    }
                }
                else if (shouldGetCompanyRatings)
                {
                    Debug.WriteLine("Ok, company ratings, right? " + answer);
                    if (answer.Equals("Yes"))
                    {
                        shouldGetCompanyRatings = false;
                        shouldSpeakCompanyRatings = true;
                    }
                    else if (answer.Equals("No"))
                    {
                        shouldGetCompanyRatings = false;
                        hasSpokenCompanyRatings = true;
                        if(hasSpokenCompanyReviews)
                        {
                            //askForNextJobOrNewSearch = true;
                            shouldAskForSaveJob = true;
                        }
                        else
                        {
                            shouldGetCompanyReviews = true;
                        }
                    }
                    else if (answer.Equals("Quit"))
                    {
                        step = 100;
                    }
                    else if (string.IsNullOrEmpty(answer) || noResponse)
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try to speak your response a little bit louder.";
                        noResponse = false;
                    }
                    else if (answer.Equals("NewSearch"))
                    {
                        step = 1;
                        jobNumber = 1;
                        newSearch = true;
                    }
                    else if (answer.Equals("Help") || answer.Equals("help"))
                    {
                        step = 101;
                    }
                    else
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try saying Yes or No.";
                    }
                }
                else if (shouldGetCompanyReviews)
                {
                    Debug.WriteLine("Would you like to know company review? " + answer);
                    if (answer.Equals("Yes"))
                    {
                        shouldGetCompanyReviews = false;
                        shouldSpeakCompanyReviews = true;
                    }
                    else if (answer.Equals("No"))
                    {
                        shouldGetCompanyReviews = false;
                        hasSpokenCompanyReviews = true;
                        if (hasSpokenCompanyRatings)
                        {
                            //askForNextJobOrNewSearch = true;
                            shouldAskForSaveJob = true;
                        }
                        else
                        {
                            shouldGetCompanyRatings = true;
                        }
                    }
                    else if (answer.Equals("Quit"))
                    {
                        step = 100;
                    }
                    else if (string.IsNullOrEmpty(answer) || noResponse)
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try to speak your response a little bit louder.";
                        noResponse = false;
                    }
                    else if (answer.Equals("NewSearch"))
                    {
                        step = 1;
                        jobNumber = 1;
                        newSearch = true;
                    }
                    else if (answer.Equals("Help") || answer.Equals("help"))
                    {
                        step = 101;
                    }
                    else
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try saying Yes or No.";
                    }
                }
                else if(askForNextJobOrNewSearch)
                {
                    if (answer == "No" || answer == "no" || answer == "NewSearch" || answer == "new search" 
                        || answer == "begin a new search")
                    {
                        step = 2;
                        jobNumber = 1;
                        askForNextJobOrNewSearch = false;
                    }
                    else if (answer.Contains("quit") || answer.Contains("Quit"))
                    {
                        step = 100;
                        askForNextJobOrNewSearch = false;
                    }
                    else if(answer.Contains("next job") || answer.Equals("NextJob"))
                    {
                        shouldSpeakNextJob = true;
                        askForNextJobOrNewSearch = false;
                        jobNumber++;
                    }
                    else if (string.IsNullOrEmpty(answer) || noResponse)
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try to speak your response a little bit louder.";
                        noResponse = false;
                    }
                    else if (answer.Equals("Help") || answer.Equals("help"))
                    {
                        step = 101;
                    }
                    else
                    {
                        previousStep = step;
                        step = 0;
                        helpText = "Try saying Next Job or New Search.";
                    }
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
                else if(answer.Equals("No") || answer.Equals("no"))
                {
                    step = 2;
                }
                else if (answer.Equals("NewSearch"))
                {
                    step = 1;
                    jobNumber = 1;
                    newSearch = true;
                }
                else if (answer.Equals("Help") || answer.Equals("help"))
                {
                    step = 101;
                }
                else
                {
                    previousStep = 100;
                    step = 0;
                    helpText = "Try saying yes or no.";
                }
                if (String.IsNullOrEmpty(answer) || noResponse)
                {
                    previousStep = 100;
                    step = 0;
                    helpText = "Try to speak your response a little bit louder.";
                    noResponse = false;
                }
            }
        }
    }
}
