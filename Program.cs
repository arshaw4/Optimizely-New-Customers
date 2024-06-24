using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NewMonthlyContacts
{
    class Program
    {
        static Stopwatch timer;
        static void Main(string[] args)
        {
            string folderName = "AppData";
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), folderName);
            string optimizelyFilePath = Path.Combine(folderPath,"OptimizelyLogin.txt");
            string constantContactFilePath = Path.Combine(folderPath, "ConstantContactLogin.txt");
            Settings settings = new Settings();
            settings.savePath = Path.Combine(folderPath,"userExport");
            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine("Fatal Error: AppData folder missing");
                try
                {
                    Directory.CreateDirectory(folderPath);
                } catch(Exception e)
                {
                    Console.WriteLine("Error creating Appdata: " + e.Message);
                    Console.ReadLine();
                    Environment.Exit(1);
                }
                Console.WriteLine("AppData created. Please input credentials in text files, save, and relaunch program");
                Console.ReadLine();
                Environment.Exit(0);
            }
            else
            {
                if (!File.Exists(optimizelyFilePath))
                {
                    Console.WriteLine("Fatal Error: Optimizely Login File Missing");
                    Console.WriteLine("Creating File, relaunch program after inputting credentials to " +optimizelyFilePath);
                    CreateOptimizelyCredsFile(optimizelyFilePath);
                    Console.ReadLine();
                    Environment.Exit(0);
                }
                else
                {
                    ReadOptimizelyLogin(optimizelyFilePath, settings);
                }
                if(!File.Exists(constantContactFilePath))
                {
                    Console.WriteLine("Constant Contact Credential File Missing");
                    Console.WriteLine("Creating File, relaunch program after inputting credentials to " + constantContactFilePath);
                    CreateConstantContactCredsFile(constantContactFilePath);
                    Console.ReadLine();
                    Environment.Exit(0);
                }
                else
                {
                    ReadConstantContactLogin(constantContactFilePath, settings);
                }
            }
            Console.WriteLine("Enter the desired month for search (1-12):");
            int month = ReadIntegerInput(1, 12);
            Console.WriteLine("Enter the desired year for search (Ex:2020):");
            int year = ReadIntegerInput(2000, DateTime.Now.Year);
            settings.savePath += "-" + month.ToString() + "-" + year.ToString() + "-" + DateTime.Now.Hour.ToString() + "-" + DateTime.Now.Minute.ToString() + "-" + DateTime.Now.Second.ToString() + ".txt";
            timer = new Stopwatch();
            timer.Start();
            GetOptimizelyToken(settings);

            JToken fullUserList = QueryAPIAllUsers(settings.optimizelyToken, month, year, settings.baseUrl);

            using (StreamWriter sw = new StreamWriter(settings.savePath))
            {
                sw.WriteLine("firstName" + "\t" + "lastName" + "\t" + "company" + "\t" + "email");
                foreach (var user in fullUserList)
                {
                    if (user != null)
                    {
                        string newLine = user["firstName"] + "\t" + user["lastName"] + "\t" + user["company"] + "\t" + user["email"];
                        sw.WriteLine(newLine);
                    }
                }
                sw.Close();
            }
            Console.WriteLine("Time to process users from ecommsite: " + timer.Elapsed.Duration().ToString());
            Console.WriteLine("Total users pulled from Ecomm ->" + fullUserList.Count().ToString());
            Console.ReadLine();
        }

        static JToken QueryAPIAllUsers(string token, int month, int year, string baseUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage responseMes = null;
                string response = "";
                Boolean timeOut = true;
                //time in ms will double every failure
                Int32 sleepTime = 10000;
                while (timeOut)
                {
                    try
                    {
                        string monthString = month.ToString();
                        string yearMadeBefore = year.ToString();
                        string monthMadeBefore = (month + 1).ToString();
                        if (month < 10)
                        {
                            monthString = "0" + month.ToString();
                            if (month < 9)
                            {
                                monthMadeBefore = "0"+(month + 1).ToString();
                            }
                        }
                        else
                        {
                            if (month == 12)
                            {
                                yearMadeBefore = (year + 1).ToString();
                                monthMadeBefore = "01";
                            }
                            monthString = month.ToString();
                        }
                        string accountMadeAfter = "createdOn ge " + year.ToString() + "-" + monthString + "-01T05:00:00+00:00";
                        string accountMadeBefore = "createdOn lt " + yearMadeBefore + "-" + monthMadeBefore + "-01T05:01:00+00:00";
                        string filter = Uri.EscapeDataString("(" + accountMadeAfter + " and " + accountMadeBefore + ")");
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://" + baseUrl  +"/api/v1/admin/userProfiles"+ "?&$count=true&$orderby=userName&$select=userName,firstName,lastName,company,phone,email&$filter=" + filter);
                        request.Headers.Add("authorization", "Bearer " + token);
                        responseMes = client.Send(request);
                        responseMes.EnsureSuccessStatusCode();
                        response = responseMes.Content.ReadAsStringAsync().Result;
                        timeOut = false;
                    }
                    catch (Exception ex)
                    {
                        timeOut = true;
                        Thread.Sleep(sleepTime);
                        sleepTime *= 2;
                        Console.WriteLine(ex.Message);
                        Console.WriteLine("Retrying Product Call");
                        Console.WriteLine("Sleeping for " + sleepTime.ToString());
                    }
                }


                if (response != null)
                {
                    if (responseMes.IsSuccessStatusCode)
                    {
                        JObject users = JObject.Parse(response);
                        Console.WriteLine("Found " + users["@odata.count"] + " Users");
                        return users["value"];
                    }
                }

                return null;
            }
        }

        static int ReadIntegerInput(int minValue, int maxValue)
        {
            int value;
            while (true)
            {
                if (int.TryParse(Console.ReadLine(), out value))
                {
                    if (value >= minValue && value <= maxValue)
                    {
                        break; // Valid input, exit the loop
                    }
                    else
                    {
                        Console.WriteLine($"Please enter a number between {minValue} and {maxValue}:");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter a valid number:");
                }
            }
            return value;
        }

        static void GetOptimizelyToken(Settings settings)
        {
                IdentityStruct identStruct = new IdentityStruct();
                identStruct.grant_type = "password";
                identStruct.username = "admin_" + settings.optimizelyUsername;
                identStruct.password = settings.optimizelyPassword;
                identStruct.scope = "isc_admin_api offline_access";

                using (HttpClient client = new HttpClient())
                {
                    //it is unlikely but this handles a timeout or rate limit on single token request
                    HttpResponseMessage responseMes = null;
                    Boolean timeOut = true;
                    Int32 sleepTime = 100;
                    Int32 maxSleepTime = 10000;
                    while (timeOut)
                    {
                        try
                        {
                            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://"+settings.baseUrl+ "/identity/connect/token");
                            request.Headers.Add("authorization", "Basic aXNjX2FkbWluOkY2ODRGQzk0LUIzQkUtNEJDNy1COTI0LTYzNjU2MTE3N0M4Rg==");
                            request.Content = new StringContent(identStruct.ToString());
                            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                            responseMes = client.Send(request);
                            responseMes.EnsureSuccessStatusCode();
                            JObject JSON = JObject.Parse(responseMes.Content.ReadAsStringAsync().Result);
                            string token = (string)JSON["access_token"];
                            timeOut = false;
                            settings.optimizelyToken = token;
                        }
                        catch (Exception e)
                        {
                        if (responseMes == null)
                        {
                            Console.WriteLine("Not an API");
                            Console.ReadLine();
                            Environment.Exit(1);
                        }
                            if ((int)responseMes.StatusCode == 400)
                            {
                                Console.WriteLine("Incorrect Credentials");
                            Console.ReadLine();
                            Environment.Exit(1);
                            }
                            if (sleepTime > maxSleepTime)
                            {
                                Console.WriteLine("Fatal Error: API unresponsive");
                            Console.ReadLine();
                            Environment.Exit(1);
                            }
                            timeOut = true;
                            Console.Write("Token Request Failed: ");
                            Console.WriteLine(e.Message);
                            Console.WriteLine("Retrying in " + (sleepTime / 1000).ToString() + "seconds");
                            Thread.Sleep(sleepTime);
                            sleepTime *= 2;
                        }

                    }
                }
        }

        static void ReadConstantContactLogin(string constantContactFilePath, Settings settings)
        {
            Dictionary<string, string> credentials = ReadCredentialsFromFile(constantContactFilePath);
            if (credentials.ContainsKey("username") && credentials.ContainsKey("password"))
            {
                settings.constantContactUser = credentials["username"];
                settings.constantContactPassword= credentials["password"];
                settings.spreadsheetMode = false;
            }
            else
            {
                Console.WriteLine("Incorrect format for " + constantContactFilePath);
                Console.WriteLine("Running in spreadsheet output mode");
                Console.WriteLine("File should be in format:");
                Console.WriteLine("username:YourUsername");
                Console.WriteLine("password:YourPassword");
                settings.spreadsheetMode = true;
            }
        }

        static void ReadOptimizelyLogin(string optimizelyFilePath, Settings settings)
        {
            Dictionary<string, string> credentials = ReadCredentialsFromFile(optimizelyFilePath);
            if (credentials.ContainsKey("username") && credentials.ContainsKey("password") && credentials.ContainsKey("url"))
            {
                settings.optimizelyUsername = credentials["username"];
                settings.optimizelyPassword = credentials["password"];
                settings.baseUrl = credentials["url"];
            }
            else
            {
                Console.WriteLine("Incorrect format for " + optimizelyFilePath);
                Console.WriteLine("File should be in format:");
                Console.WriteLine("username:YourUsername");
                Console.WriteLine("password:YourPassword");
                Console.WriteLine("url:CompanyURL");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        static Dictionary<string, string> ReadCredentialsFromFile(string filePath)
        {
            Dictionary<string, string> credentials = new Dictionary<string, string>();
            try
            {
                using(StreamReader sr = new StreamReader(filePath))
                {
                    string line;
                    while((line = sr.ReadLine())!= null)
                    {
                        line = line.Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            string[] parts = line.Split(':');
                            if(parts.Length == 2)
                            {
                                string key = parts[0].Trim().ToLower();
                                string value = parts[1].Trim();
                                credentials[key] = value;
                            }
                        }
                    }
                }
            } catch(Exception ex)
            {
                Console.WriteLine("Error while reading " + filePath);
                Console.ReadLine();
                Environment.Exit(1);
            }
            return credentials;
        }


        static void CreateOptimizelyCredsFile(string filePath)
        {
            try
            {
                File.Create(filePath).Close();
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("username:");
                    writer.WriteLine("password:");
                    writer.WriteLine("url:");
                }
            } catch(Exception e)
            {
                Console.WriteLine("Error creating " + filePath + ": " + e.Message);
                Console.ReadLine();
                Environment.Exit(1);
            }
            Console.WriteLine("Created Optimizely Login File");
        }
        static void CreateConstantContactCredsFile(string filePath)
        {
            try
            {
                File.Create(filePath).Close();
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("username:");
                    writer.WriteLine("password:");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error creating " + filePath + ": " + e.Message);
                Console.ReadLine();
                Environment.Exit(1);
            }
            Console.WriteLine("Created Optimizely Login File");
        }
    }
}