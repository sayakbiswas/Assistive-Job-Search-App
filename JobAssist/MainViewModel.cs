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
using Indeed;
using RestSharp;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using System.Windows;

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

        public bool searchByLocation = false;

        public List<Job> jobs = new List<Job>();

        
        

       

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
            StartDialogueCommand = new RelayCommand(Dialogue);


            //Initialize recognition engine
            InitializeRecognitionEngine();
            InitializeSynthesizer();
        

           // Dialogue();

        }

        //Disregard this  -  only used to test API without walking through speech process
        private void api_test()
        {

            var client = new RestClient("http://www.glassdoor.com/api/json/search/jobProgression.htm");
            var request = new RestRequest(Method.GET);
            request.AddParameter("t.p", "102234");
            request.AddParameter("t.k", "egSVvV0B2Jg");
            request.AddParameter("format", "json");
            request.AddParameter("v", "1");
            request.AddParameter("action", "jobs-stats"); //job search query string
           // request.AddParameter("q", "software developer");
            request.AddParameter("jobTitle", "cashier");
            request.AddParameter("countryId", "1");

            IRestResponse response = client.Execute(request);


            JObject jobsData = JObject.Parse(response.Content);

           string medianSalary = (string)jobsData["response"]["payMedian"];

            Debug.WriteLine(medianSalary);


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


                builder.AppendText("Welcome to job assist. Speak your responses after the beep. Say quit at any time to exit.");

                builder.StartSentence();

                builder.AppendText("Would you like to search for jobs today? ");

                builder.EndSentence();

                builder.AppendBookmark("1");


            }

            if (step == 2)
            {
                builder.StartSentence();

                builder.AppendText("What type of job would you like to search for?");

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

                builder.AppendText("What is the city, state or zip code that you would like to search?");

                builder.EndSentence();

                builder.AppendBookmark("5");
            }


            if (step == 6)
            {
                builder.StartSentence();

                string jobText = string.Format("You would like to search for jobs in {0}", answer);
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
                    _recognizer.Recognize();

                    if (answer == "Yes" || answer == "yes")
                    {
                        //save job
                    }

                    Thread.Sleep(1500);


                    _synthesizer.Speak("Would you like to get salary information for this job?");

                    Console.Beep();
                    _recognizer.Recognize();

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



                    _synthesizer.Speak("Would you like to hear the next job or begin a new search?");

                    Console.Beep();
                    _recognizer.Recognize();

                    if (answer == "No" || answer == "no" || answer == "new search" || answer == "begin a new search")
                    {
                        step = 2;
                        Dialogue();
                    }


                    jobNumber++;

                }

            }


            if (step == 100)
            {

                builder.StartSentence();

                builder.AppendText("Thank you for using Job Assist. Goodbye.");

                builder.EndSentence();

                Application.Current.Shutdown();
            }

            _synthesizer.Speak(builder);


        }

        private void InitializeSynthesizer()
        {
            _synthesizer = new SpeechSynthesizer();

            _synthesizer.SetOutputToDefaultAudioDevice();

            _synthesizer.BookmarkReached += BookmarkReached;
        }

        private void BookmarkReached(object sender, BookmarkReachedEventArgs e)
        {
            Debug.WriteLine(e.Bookmark);

            if(e.Bookmark != "7" && !(e.Bookmark == "8" && Convert.ToInt32(jobSearchResults) == 0 ))
            {
                Console.Beep();
                _recognizer.Recognize();

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
                Debug.WriteLine("Would you like to search for jobs today: " + answer);
                if (answer == "Yes" || answer == "yes")
                {
                    step = 2;

                }
                else if (answer == "No" || answer == "no")
                {
                    step = 1;
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
                Debug.WriteLine("What type of job would you like to search for: " + answer);
                step = 3;

            }


            if (e.Bookmark == "3")
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
                    previousStep = 2;
                    helpText = "Try saying yes or no.";
                }

            }

            if (e.Bookmark == "4")
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

            if (e.Bookmark == "5")
            {
                Debug.WriteLine("You would like to search for jobs in: " + answer);
                step = 6;

            }

            if (e.Bookmark == "6")
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
            _recognizer = new SpeechRecognitionEngine();

            _recognizer.SetInputToDefaultAudioDevice();

            _recognizer.LoadGrammar(new DictationGrammar());

            //Cap response time to 4 seconds
            _recognizer.BabbleTimeout = TimeSpan.FromSeconds(4);


            //Set to timeout if nothing is said in 3 seconds
            _recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(3);

            _recognizer.RecognizeCompleted += RecognizeCompleted;

            _recognizer.SpeechRecognized += SpeechRecognized;
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
    }
}
