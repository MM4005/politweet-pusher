using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Core.Enum;
using Tweetinvi.Core.Events.EventArguments;

namespace TwitterFeed
{
    class Program
    {
        static int wait;
        static int count = 0;
        static DateTime time;
        static DateTime last;
        static MySqlConnection con;
        static int[] stateValues;

#if DEBUG
        static string partialTweet;
        static bool inputValues = true;
#endif

        static void Main(string[] args)
        {
#if DEBUG
            Console.Write("Update to database? (y/n): ");
            ConsoleKey key;
            do
            {
                key = Console.ReadKey(true).Key;
            } while (key != ConsoleKey.Y && key != ConsoleKey.N);
            if (key == ConsoleKey.N)
            {
                inputValues = false;
            }
            Console.Clear();
#endif
            Console.Write("Update every __ seconds: ");
            wait = Convert.ToInt32(Console.ReadLine());
            if (wait <= 0)
            {
                Environment.Exit(0);
            }
            Console.WriteLine("Started");
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => { return true; };
            Auth.SetUserCredentials("CONSUMER KEY", "CONSUMER SECRET", "USER ACCESS TOKEN", "USER ACCESS SECRET");
            Console.WriteLine("Added Auth, starting task.");
            stateValues = new int[50];
            Task r = run();
            Task.Run(Connection);
            r.Wait();
        }

        static async Task run()
        {
            Console.WriteLine("Task working");
            string connectionString =
          "Server=localhost;" +
          "Database=politics;" +
          "User ID=root;" +
          "Password=root;" +
          "Pooling=false";
            con = new MySqlConnection(connectionString);
            var s = Stream.CreateFilteredStream(Auth.Credentials);
            s.AddTweetLanguageFilter(Language.English);
            s.AddTrack("Trump");
            s.AddTrack("Clinton");
            s.AddTrack("Sanders");
            s.AddTrack("Cruz");
            s.AddTrack("Kasich");
            s.AddTrack("democrat");
            s.AddTrack("republican");
            s.FilterLevel = Tweetinvi.Core.Interfaces.Streaminvi.Parameters.StreamFilterLevel.None;
            s.MatchingTweetReceived += Recieved;
            time = DateTime.Now;
            s.StreamStopped += S_StreamStopped;
            Console.WriteLine("Starting stream");
            while (true)
            {
                await s.StartStreamMatchingAllConditionsAsync();
                Console.WriteLine("Restarting stream");
                Thread.Sleep(5000);
            }
        }

        static Task Connection()
        {
#if DEBUG
            if (inputValues)
            {
                con.Open();
            }
#else
            con.Open();
#endif
            while (true)
            {
                last = DateTime.Now;
                string sql = "INSERT INTO party " +
                            "(`AL`,`AK`,`AZ`,`AR`,`CA`,`CO`,`CT`,`DE`,`FL`,`GA`,`HI`,`ID`,`IL`,`IN`,`IA`,`KS`,`KY`,`LA`,`ME`,`MD`,`MA`,`MI`,`MN`,`MS`,`MO`,`MT`,`NE`,`NV`,`NH`,`NJ`,`NM`,`NY`,`NC`,`ND`,`OH`,`OK`,`OR`,`PA`,`RI`,`SC`,`SD`,`TN`,`TX`,`UT`,`VT`,`VA`,`WA`,`WV`,`WI`,`WY`) " +
                            "VALUES (";
                while (DateTime.Now.Subtract(last).TotalSeconds < wait) ;
                lock (stateValues)
                {
                    for (int i = 0; i < stateValues.Length; i++)
                    {
                        if (i > 0)
                            sql += ",";
                        if (stateValues[i] == 0)
                        {
                            sql += "NULL";
                        }
                        else
                        {
                            sql += stateValues[i];
                        }
                    }
                    sql += ")";
                    stateValues = new int[50];
                }
#if DEBUG
                if (inputValues)
                {
                    MySqlCommand command = con.CreateCommand();
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
#else
                MySqlCommand command = con.CreateCommand();
                command.CommandText = sql;
                command.ExecuteNonQuery();
#endif
            }
        }

        private static void S_StreamStopped(object sender, StreamExceptionEventArgs e)
        {
            Console.WriteLine("Disconnect");
            if (e.DisconnectMessage != null)
            {
                Console.WriteLine(e.DisconnectMessage.Code.ToString());
                if (e.DisconnectMessage.Reason != null)
                {
                    Console.WriteLine(e.DisconnectMessage.Reason.ToString());
                }
                else
                {
                    Console.WriteLine("No reason");
                }
                if (e.DisconnectMessage.StreamName != null)
                {
                    Console.WriteLine(e.DisconnectMessage.StreamName.ToString());
                }
                else
                {
                    Console.WriteLine("No stream name");
                }

            }
            else
            {
                Console.WriteLine("No disconnect message");
            }
            if (e.Exception != null && e.Exception.Message != null)
            {
                Console.WriteLine(e.Exception.Message.ToString());
                Exception ex = e.Exception.InnerException;
                while (ex != null && ex.Message != null)
                {
                    Console.WriteLine(ex.Message);
                    ex = ex.InnerException;
                }
            }
            else
            {
                Console.WriteLine("No exception");
            }

            Console.WriteLine("Message done");
        }

        static void Recieved(object sender, MatchedTweetReceivedEventArgs args)
        {
            if (args.Tweet.CreatedBy.Location != null)
            {
                int? state = GetState.Find(args.Tweet.CreatedBy.Location);
                if (state != null)
                {
                    bool isDemo = args.MatchingTracks.Any(t => new string[] { "sanders", "clinton", "democrat" }.Contains(t));
                    bool isRepub = args.MatchingTracks.Any(t => new string[] { "cruz", "kasich", "trump", "republican" }.Contains(t));
                    string candidate = args.MatchingTracks.First();
                    candidate = char.ToUpper(candidate[0]) + candidate.Substring(1);
                    if (isDemo != isRepub)
                    {
                        Party p;
                        if (isDemo)
                        {
                            stateValues[state.Value]++;
                        }
                        else
                        {
                            stateValues[state.Value]--;
                        }
                        count++;
                        DateTime current = DateTime.Now;
                        Console.Clear();
                        TimeSpan elapsed = current.Subtract(time);
                        Console.WriteLine("Time elapsed: " + Math.Truncate(elapsed.TotalSeconds));
                        Console.WriteLine("Collected: " + count);
                        Console.WriteLine("Permin: " + count / elapsed.TotalSeconds * 60);
#if DEBUG
                        if (partialTweet == null || count % 25 == 0)
                        {
                            partialTweet = args.Tweet.Text;
                        }
                        Console.WriteLine("\n" + partialTweet);
#endif
                    }
                }
            }
        }
    }

    static class GetState
    {
        #region states
        private static Dictionary<string, string> states = new Dictionary<string, string>()
        {
            {"al", "alabama"},
            {"ak", "alaska"},
            {"az", "arizona"},
            {"ar", "arkansas"},
            {"ca", "california"},
            {"co", "colorado"},
            {"ct", "connecticut"},
            {"de", "delaware"},
            {"fl", "florida"},
            {"ga", "georgia"},
            {"hi", "hawaii"},
            {"id", "idaho"},
            {"il", "illinois"},
            {"in", "indiana"},
            {"ia", "iowa"},
            {"ks", "kansas"},
            {"ky", "kentucky"},
            {"la", "louisiana"},
            {"me", "maine"},
            {"md", "maryland"},
            {"ma", "massachusetts"},
            {"mi", "michigan"},
            {"mn", "minnesota"},
            {"ms", "mississippi"},
            {"mo", "missouri"},
            {"mt", "montana"},
            {"ne", "nebraska"},
            {"nv", "nevada"},
            {"nh", "new hampshire"},
            {"nj", "new jersey"},
            {"nm", "new mexico"},
            {"ny", "new york"},
            {"nc", "north carolina"},
            {"nd", "north dakota"},
            {"oh", "ohio"},
            {"ok", "oklahoma"},
            {"or", "oregon"},
            {"pa", "pennsylvania"},
            {"ri", "rhode island"},
            {"sc", "south carolina"},
            {"sd", "south dakota"},
            {"tn", "tennessee"},
            {"tx", "texas"},
            {"ut", "utah"},
            {"vt", "vermont"},
            {"va", "virginia"},
            {"wa", "washington"},
            {"wv", "west virginia"},
            {"wi", "wisconsin"},
            {"wy", "wyoming"}
        };
        #endregion
        public static int? Find(string data)
        {
            data = new string(data.Where(c => !char.IsPunctuation(c)).ToArray()).ToLower();
            for (int i = 0; i < states.Count; i++)
            {
                KeyValuePair<string, string> item = states.ElementAt(i);
                if (data.Contains(item.Value))
                {
                    return i;
                }
            }
            string[] parts = data.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                try
                {
                    KeyValuePair<string, string> s = states.First(x => x.Key == parts[i]);
                    for (int j = 0; j < states.Count; j++)
                    {
                        KeyValuePair<string, string> item = states.ElementAt(j);
                        if (s.Key == item.Key)
                        {
                            return j;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            return null;
        }
    }

    public enum Party
    {
        Democrat,
        Republican
    }
}
