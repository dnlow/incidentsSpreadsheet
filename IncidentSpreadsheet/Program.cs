using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace IncidentSpreadsheet
{
    class Program
    {
        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/sheets.googleapis.com-dotnet-quickstart.json
        static string[] Scopes = { SheetsService.Scope.Spreadsheets };
        static string ApplicationName = "IncidentSpreadsheet";
        static string LogFolderPath = "//home/coreyf/gst_dashboard/data";
        static string TestFolderPath = "C://cygwin/home/SLUGIS/documents"; // Change this for retesting in development if not on H15KS52

        static SheetsService service;

        // Change this in the event a new spreadsheet is created and wanted to be used.
        static string spreadsheetID = "12A2DN5RlawDzPz_RfX5jN3YPNSNc6-xD8nVpwqS09is";

        static void Main(string[] args)
        {
            string sheetName, dataPath;
            if (args.Length == 2)
            {
                if (args[1] == "1")
                {
                    sheetName = "TestSheet";
                    dataPath = TestFolderPath;
                }
                else
                {
                    Console.WriteLine("Incorrect number of arguments.\nusage: incidentSpreadsheet\ntesting usage: incidentSpreadsheet 1");
                    return;
                }
            }
            else if (args.Length > 3)
            {
                //bad
                Console.WriteLine("Incorrect number of arguments.\nusage: incidentSpreadsheet\ntesting usage: incidentSpreadsheet 1");
                return;
            }
            else
            {
                sheetName = "GoogleSheet";
                dataPath = LogFolderPath;
            }
            sheetName = "TestSheet";
            dataPath = TestFolderPath;

            // Populate the incidents
            Console.WriteLine("Grabbing incident information...");
            Incidents incidents = new Incidents();
            incidents.PopulateIncidents(dataPath);

            service = CreateService("slugis@slugis-186423.iam.gserviceaccount.com", "C:/Users/SLUGIS/Documents/DaneITDevelopment/IncidentSpreadsheet/IncidentSpreadsheet/SLUGIS-7c7a36e70ba8.p12");

            Console.WriteLine("Executing...");
            // Create Append Request
            SpreadsheetsResource.ValuesResource.AppendRequest appendRequest = GetAppendRequest(sheetName, incidents);

            Console.WriteLine("Incidents to be added: {0}", incidents.GetIncidents().Count);

            // Execute
            appendRequest.Execute();
        }

        private static SpreadsheetsResource.ValuesResource.AppendRequest GetAppendRequest(string sheetName, Incidents incidents)
        {
            SpreadsheetsResource.ValuesResource.AppendRequest request;
            string range = sheetName + "!A:A";
            
            request = service.Spreadsheets.Values.Append(incidents.GenerateDataBlock(), spreadsheetID, range);
            request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;

            return request;
        }

        private static SheetsService CreateService(string serviceAccountEmail, string keyFilePath)
        {
            if (!File.Exists(keyFilePath))
            {
                Console.WriteLine("An error occurered - key file does not exist.");
                return null;
            }

            string[] scopes = { SheetsService.Scope.Spreadsheets };

            var certificate = new X509Certificate2(keyFilePath, "notasecret", X509KeyStorageFlags.Exportable);
            try
            {
                ServiceAccountCredential credential = new ServiceAccountCredential(
                    new ServiceAccountCredential.Initializer(serviceAccountEmail)
                    {
                        Scopes = scopes
                    }.FromCertificate(certificate));

                SheetsService service = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName
                });

                return service;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.InnerException);
                return null;
            }
        }
    }

    class Incidents
    {
        List<Incident> incidents;

        public Incidents()
        {
            incidents = new List<Incident>();
        }

        public void PopulateIncidents(string directory)
        {
            string[] filePaths = Directory.GetFiles(directory, "*_Log.txt");

            foreach (string file in filePaths)
            {
                if (DateTime.Compare(ConvertFilenameToDatetime(file), DateTime.Today.AddDays(-1)) >= 0)
                {
                    AddIncident(file);
                }
            }
        }

        private void AddIncident(string filepath)
        {
            string line;
            Incident temp;
            using (StreamReader reader = new StreamReader(filepath))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Split(new[] { '|' }, StringSplitOptions.None);
                    try
                    {
                        if (fields[5] == "FOAW" || fields[5].Contains("FWL") || (fields[5].Contains("FVC") && fields[5].Contains("W"))
                            || fields[5] == "FOO" || fields[5] == "FOD" || fields[5] == "FSRW" || fields[5] == "MTC" || fields[5] == "FVP")
                        {
                            temp = new Incident
                            {
                                details = fields[6],
                                latitude = fields[7],
                                longitude = fields[8],
                                time = fields[4],
                                event_id = fields[1],
                                incident_id = fields[2],
                                type = fields[5]
                            };
                            incidents.Add(temp);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        public List<Incident> GetIncidents()
        {
            return incidents;
        }

        public ValueRange GenerateDataBlock()
        {
            ValueRange data = new ValueRange
            {
                Values = new List<IList<object>> { },
                MajorDimension = "ROWS"
            };

            foreach (Incident inc in incidents)
            {
                data.Values.Add(inc.GetValues());
            }

            return data;
        }

        /// <summary>
        ///     Janky ass function to convert the filename/path and grab the year month and date from it
        ///     Could probably be done with a regex.
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        private DateTime ConvertFilenameToDatetime(string filepath)
        {
            string[] yearString = filepath.Split('_'); // [0] - path+year, [1] - MMdd

            yearString[0] = yearString[0].Remove(0, yearString[0].Length - 4); // remove path and leave year

            char[] monthDateParts = yearString[1].ToCharArray(); // [0-1] MM, [2-3] dd

            char[] monthString = { monthDateParts[0], monthDateParts[1] };
            char[] dayString = { monthDateParts[2], monthDateParts[3] };

            Int32.TryParse(yearString[0].ToString(), out int year);
            Int32.TryParse(new string(monthString), out int month);
            Int32.TryParse(new string(dayString), out int day);

            DateTime date = new DateTime(year, month, day);

            return date;
        }
    }

    class Incident
    {
        public string event_id = " "; // 1st item in parsed line
        public string incident_id = " "; // 2nd item
        public string category = " "; // 5th item + key'd values of details
        public string details = " "; // 6th
        public string type = " "; // 5th item
        public string address = " "; // 9th item
        public string jurisdiction = " "; // 10th item
        public string latitude = " "; // 7th
        public string longitude = " "; // 7th
        public string time = " "; // 4th - format as %Y%m%d%H%M%S

        public Incident()
        {

        }

        public string[] GetValues()
        {
            return new string[]
            {
                time,
                event_id,
                incident_id,
                category,
                details,
                type,
                address,
                jurisdiction,
                latitude,
                longitude
            };
        }

        public Dictionary<string, string[]> GetTypeDictionary()
        {
            return new Dictionary<string, string[]>{
                {"DEL",  new string[] { "Other", "Delay Response"}},
                {"FAA",  new string[] { "Fire", "Aircraft"}},
                {"FAAL",  new string[] { "Fire", "Aircraft - Large"}},
                {"FAAS",  new string[] { "Fire", "Aircraft - Small"}},
                {"FCS",  new string[] { "Fire", "Smoke Check"}},
                {"FFA",  new string[] { "Fire", "False Alarm"}},
                {"FFAS",  new string[] { "Fire", "False Alarm Smoke"}},
                {"FOA",  new string[] { "Fire", "Assist"}},
                {"FOAI",  new string[] { "Fire", "Assist-Instant Aid"}},
                {"FOAM",  new string[] { "Fire", "Assist-Mutual Aid"}},
                {"FOAS",  new string[] { "Fire", "Mutual-Aid Struct"}},
                {"FOAV",  new string[] { "Fire", "Vehicle Assist"}},
                {"FOAW",  new string[] { "Fire", "Wildland Assist"}},
                {"FOD",  new string[] { "Fire", "Debris"}},
                {"FODCB",  new string[] { "Fire", "Check Control Burn"}},
                {"FODI",  new string[] { "Fire", "Debris Illegal"}},
                {"FODR",  new string[] { "Fire", "Debris - Roadside"}},
                {"FOI",  new string[] { "Fire", "Improvement"}},
                {"FOO",  new string[] { "Fire", "Other"}},
                {"FOOA",  new string[] { "Fire", "Agriculture"}},
                {"FOOAH",  new string[] { "Fire", "Almond Hulls"}},
                {"FOOCM",  new string[] { "Fire", "Cotton Module"}},
                {"FOOD",  new string[] { "Fire", "Dumpster"}},
                {"FOOHS",  new string[] { "Fire", "Haystack"}},
                {"FOOP",  new string[] { "Fire", "Pole"}},
                {"FSC",  new string[] { "Fire", "Commercial"}},
                {"FSCA",  new string[] { "Fire", "Commercial Alarm"}},
                {"FSCM",  new string[] { "Fire", "STR Marina and Boat"}},
                {"FSCR",  new string[] { "Fire", "3rd Alarm into RDN"}},
                {"FSCW",  new string[] { "Fire", "Com Struct Wildland"}},
                {"FSM",  new string[] { "Fire", "Multi Family"}},
                {"FSMA",  new string[] { "Fire", "Multi Family Alarm"}},
                {"FSO",  new string[] { "Fire", "Structure Other"}},
                {"FSOF",  new string[] { "Fire", "Structure Flue"}},
                {"FSOP",  new string[] { "Fire", "Structure Pier"}},
                {"FSOU",  new string[] { "Fire", "Type Unknown"}},
                {"FSR",  new string[] { "Fire", "Residential"}},
                {"FSRA",  new string[] { "Fire", "Residential Alarm"}},
                {"FSRC",  new string[] { "Fire", "Chimney"}},
                {"FSRO",  new string[] { "Fire", "Oven"}},
                {"FSRW",  new string[] { "Fire", "Res Struct Wildland"}},
                {"FVC",  new string[] { "Fire", "Veh Commercial"}},
                {"FVCL",  new string[] { "Fire", "Large Vehicle"}},
                {"FVCLW",  new string[] { "Fire", "Large Vehicle Wildland"}},
                {"FVCT",  new string[] { "Fire", "Train"}},
                {"FVCTW",  new string[] { "Fire", "Train Threat Veg"}},
                {"FVCW",  new string[] { "Fire", "Veh Com Wildland"}},
                {"FVP",  new string[] { "Fire", "Veh Passenger"}},
                {"FVPB",  new string[] { "Fire", "Boat"}},
                {"FVPBL",  new string[] { "Fire", "Boat Large"}},
                {"FVPSC",  new string[] { "Fire", "Veh Pas Thr Com Str"}},
                {"FVPSR",  new string[] { "Fire", "Veh Pas Thr Res Str"}},
                {"FVPW",  new string[] { "Fire", "Veh Pass Wildland"}},
                {"FWL",  new string[] { "Fire", "Wildland"}},
                {"FWLCD",  new string[] { "Fire", "Center Div/Vac Lot"}},
                {"FWLG",  new string[] { "Fire", "Grass-LRA"}},
                {"FWLH",  new string[] { "Fire", "Wildland High"}},
                {"FWLL",  new string[] { "Fire", "Wildland Low"}},
                {"FWLM",  new string[] { "Fire", "Wildland Med"}},
                {"FWLMTZ",  new string[] { "Fire", "Wildland City MTZ"}},
                {"FWLT",  new string[] { "Fire", "Wildland Lightning"}},
                {"FWLZ",  new string[] { "Fire", "Wildland T Zone"}},
                {"HAS",  new string[] { "Hazard", "Haz, Aircraft"}},
                {"HAS1",  new string[] { "Hazard", "Hazard, Aircraft, Alert 1"}},
                {"HAS2",  new string[] { "Hazard", "Hazard, Aircraft, Alert 2"}},
                {"HAS3",  new string[] { "Hazard", "Hazard, Aircraft, Alert 3"}},
                {"HFS",  new string[] { "Hazard", "Haz, Fire Menace Standby"}},
                {"HFSEQ",  new string[] { "Hazard", "Haz, Earthquake"}},
                {"HFSFW",  new string[] { "Hazard", "Haz, Fireworks Complaint"}},
                {"HFSP",  new string[] { "Hazard", "Haz, Petro Spill - SM"}},
                {"HOA",  new string[] { "Hazard", "Haz, Assist"}},
                {"HOAB",  new string[] { "Hazard", "Haz, Assist BINTF"}},
                {"HOAT",  new string[] { "Hazard", "Haz, Assist BINTF"}},
                {"HOAW",  new string[] { "Hazard", "Haz, Tree"}},
                {"HSB",  new string[] { "Hazard", "Haz, Bomb Threat"}},
                {"HSBC",  new string[] { "Hazard", "Bomb Threat - Commercial"}},
                {"HSBR",  new string[] { "Hazard", "Bomb Threat - Residential"}},
                {"HSBT",  new string[] { "Hazard", "Bomb Threat - Other"}},
                {"HSBTC",  new string[] { "Hazard", "Bomb Threat, Com / MFD"}},
                {"HSBTS",  new string[] { "Hazard", "Bomb Threat - Residential"}},
                {"HSBTV",  new string[] { "Hazard", "Bomb Threat, Vehicle"}},
                {"HSE",  new string[] { "Hazard", "Haz, Electrical"}},
                {"HSG",  new string[] { "Hazard", "Haz, Gas"}},
                {"HSGC",  new string[] { "Hazard", "Haz, Gas - Commercial STR"}},
                {"HSGR",  new string[] { "Hazard", "Haz, Gas - Res STR"}},
                {"HTT",  new string[] { "Hazard", "Haz, Terrorist Threat"}},
                {"HZM",  new string[] { "Hazard", "Hazmat"}},
                {"HZM1",  new string[] { "Hazard", "Hazmat, Level 1"}},
                {"HZM2",  new string[] { "Hazard", "Hazmat, Level 2"}},
                {"HZM3",  new string[] { "Hazard", "Hazmat, Level 3"}},
                {"HZM3M",  new string[] { "Hazard", "Hazmat, L3, Mass Casualty"}},
                {"HZMCMA",  new string[] { "Hazard", "Car Mon Alarm Sounding"}},
                {"HZMDL",  new string[] { "Hazard", "Hazmat, Drug Lab"}},
                {"HZMEX",  new string[] { "Hazard", "Explosion"}},
                {"HZMF",  new string[] { "Hazard", "Haz, Flam. Liquid"}},
                {"HZMMC",  new string[] { "Hazard", "Hazmat, Mass Casualty"}},
                {"LEB",  new string[] { "Law Enforcement", "LE, Arson Bomb"}},
                {"LEBK",  new string[] { "Law Enforcement", "LE, Arson Bomb, K9"}},
                {"LEI",  new string[] { "Law Enforcement", "LE, Investigation"}},
                {"LEIJ",  new string[] { "Law Enforcement", "LE, Investigation, JDSF"}},
                {"LEO",  new string[] { "Law Enforcement", "LE, Other"}},
                {"LEOAOA",  new string[] { "Law Enforcement", "LE, Assist Other Agency"}},
                {"LEOCE",  new string[] { "Law Enforcement", "LE, Code Enforcement"}},
                {"LEOF",  new string[] { "Law Enforcement", "LE, Fireworks Complaint"}},
                {"LEOJ",  new string[] { "Law Enforcement", "LE, JDSF"}},
                {"LEOMON",  new string[] { "Law Enforcement", "LE, Monitoring/Transport"}},
                {"MED",  new string[] { "Medical", "Medical"}},
                {"MED01",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED01A",  new string[] { "Medical", "MEDICAL - C2 - ABD Pain"}},
                {"MED02",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED03",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED03A",  new string[] { "Medical", "MEDICAL - C2 - Animal Bite"}},
                {"MED04",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED05",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED05A",  new string[] { "Medical", "MEDICAL - C2 - Back Pain"}},
                {"MED06",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED07",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED08",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED09",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED1",  new string[] { "Medical", "Medical, Priority 1"}},
                {"MED10",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED11",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED12",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED12A",  new string[] { "Medical", "MEDICAL - C2 - Seizures"}},
                {"MED13",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED13A",  new string[] { "Medical", "MEDICAL - C2 - Diabetic"}},
                {"MED14",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED15",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED16",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED17",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED17A",  new string[] { "Medical", "MEDICAL - C2 - FALLS"}},
                {"MED18",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED19",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED2",  new string[] { "Medical", "Medical, Priority 2"}},
                {"MED20",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED20A",  new string[] { "Medical", "MEDICAL - C2 - Exposure"}},
                {"MED21",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED21A",  new string[] { "Medical", "MEDICAL - C2 - Hemorrhage"}},
                {"MED22",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED23",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED24",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED25",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED26",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED26A",  new string[] { "Medical", "MEDICAL - C2 - Sick Person"}},
                {"MED27",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED28",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED29",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED3",  new string[] { "Medical", "Medical, Priority 3"}},
                {"MED30",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED30A",  new string[] { "Medical", "MEDICAL - C2 - Traumatic"}},
                {"MED31",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED31A",  new string[] { "Medical", "MEDICAL - C2 - Unconscious"}},
                {"MED32",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED33",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED34",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED35",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED36",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED37",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED38",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED39",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED4",  new string[] { "Medical", "Medical, Priority 4"}},
                {"MED40",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED41",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED42",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED43",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED44",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED45",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED46",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED47",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED48",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED49",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED5",  new string[] { "Medical", "Medical, Priority 5"}},
                {"MED50",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED51",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED515",  new string[] { "Medical", "5150"}},
                {"MED52",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED53",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED54",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED55",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED56",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED57",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED58",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED59",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED6",  new string[] { "Medical", "Medical, Priority 6"}},
                {"MED60",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED61",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED62",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED63",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED64",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED65",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED66",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED67",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED68",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED69",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED7",  new string[] { "Medical", "Medical, Priority 7"}},
                {"MED70",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED71",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED72",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED73",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED74",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED75",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED76",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED77",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED78",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED79",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED80",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED81",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED82",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED83",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED84",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED85",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED86",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED87",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED88",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED89",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED90",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED91",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED92",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED93",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED94",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED95",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED96",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED97",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED98",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MED99",  new string[] { "Medical", "PRO QA, Reserved"}},
                {"MEDA",  new string[] { "Medical", "Medical, Alpha"}},
                {"MEDABD",  new string[] { "Medical", "Medical, Abdominal Pain"}},
                {"MEDAD",  new string[] { "Medical", "Aircraft Down"}},
                {"MEDAL",  new string[] { "Medical", "Aircraft Down, Large"}},
                {"MEDAM",  new string[] { "Medical", "Ambulance Request"}},
                {"MEDAS",  new string[] { "Medical", "Aircraft Down, Small"}},
                {"MEDASL",  new string[] { "Medical", "Assault"}},
                {"MEDAVL",  new string[] { "Medical", "Avalanche"}},
                {"MEDB",  new string[] { "Medical", "Medical, Bravo"}},
                {"MEDBIR",  new string[] { "Medical", "Child Birth"}},
                {"MEDBIT",  new string[] { "Medical", "Bites or Stings"}},
                {"MEDBLD",  new string[] { "Medical", "Bleeding"}},
                {"MEDBP",  new string[] { "Medical", "Blood Pressure"}},
                {"MEDBRN",  new string[] { "Medical", "Burns"}},
                {"MEDC",  new string[] { "Medical", "Charlie"}},
                {"MEDC2",  new string[] { "Medical", "Code 2"}},
                {"MEDC3",  new string[] { "Medical", "Code 3"}},
                {"MEDCD",  new string[] { "Medical", "Child Down"}},
                {"MEDCHK",  new string[] { "Medical", "Choking"}},
                {"MEDCM",  new string[] { "Medical", "Carbon Monoxide"}},
                {"MEDCO",  new string[] { "Medical", "Coastal"}},
                {"MEDCOS",  new string[] { "Medical", "Comp of Surgery"}},
                {"MEDCP",  new string[] { "Medical", "Chest Pains"}},
                {"MEDCT1",  new string[] { "Medical", "Aircraft Category 1"}},
                {"MEDCT2",  new string[] { "Medical", "Aircraft Category 2"}},
                {"MEDCT3",  new string[] { "Medical", "Aircraft Category 3"}},
                {"MEDCT4",  new string[] { "Medical", "Aircrft SNGL Offsite"}},
                {"MEDCT5",  new string[] { "Medical", "Aircrft MULT Offsite"}},
                {"MEDD",  new string[] { "Medical", "Medical, Delta"}},
                {"MEDDB",  new string[] { "Medical", "Difficulty Breathing"}},
                {"MEDDIA",  new string[] { "Medical", "Diabetic"}},
                {"MEDDRW",  new string[] { "Medical", "Drowning"}},
                {"MEDE",  new string[] { "Medical", "Medical Echo"}},
                {"MEDELT",  new string[] { "Medical", "Electrocution"}},
                {"MEDEYE",  new string[] { "Medical", "Eye Injury"}},
                {"MEDF",  new string[] { "Medical", "Flight"}},
                {"MEDFAL",  new string[] { "Medical", "Fall"}},
                {"MEDFF",  new string[] { "Medical", "Flight Following"}},
                {"MEDFM",  new string[] { "Medical", "Flight Missed"}},
                {"MEDFNT",  new string[] { "Medical", "Fainted / Passed Out"}},
                {"MEDFSI",  new string[] { "Medical", "SSV Inquiry"}},
                {"MEDFSM",  new string[] { "Medical", "SSV Missed Flight"}},
                {"MEDFSS",  new string[] { "Medical", "SSV Scene Flight"}},
                {"MEDH",  new string[] { "Medical", "Heart Attack, Stroke"}},
                {"MEDHET",  new string[] { "Medical", "Heat Related"}},
                {"MEDI",  new string[] { "Medical", "Industrial Accident"}},
                {"MEDINQ",  new string[] { "Medical", "Inquiry"}},
                {"MEDINT",  new string[] { "Medical", "Interfacility"}},
                {"MEDL",  new string[] { "Medical", "Life Threatening"}},
                {"MEDLG",  new string[] { "Medical", "Lifeguard"}},
                {"MEDLOC",  new string[] { "Medical", "Level of Consciousness"}},
                {"MEDM",  new string[] { "Medical", "Mass-Casualty"}},
                {"MEDN",  new string[] { "Medical", "Non-Life Threatening"}},
                {"MEDO",  new string[] { "Medical", "Medical, Omega"}},
                {"MEDOD",  new string[] { "Medical", "Overdose"}},
                {"MEDOTS",  new string[] { "Medical", "TC Over the Side"}},
                {"MEDPD",  new string[] { "Medical", "Person Down"}},
                {"MEDPRG",  new string[] { "Medical", "Comp of Pregnancy"}},
                {"MEDR",  new string[] { "Medical", "EMS Relay"}},
                {"MEDRA",  new string[] { "Medical", "Ringing Alarm"}},
                {"MEDRI",  new string[] { "Medical", "Ambulance Ride In"}},
                {"MEDS",  new string[] { "Medical", "Standby"}},
                {"MEDSOW",  new string[] { "Medical", "Slumped Over Wheel"}},
                {"MEDSTG",  new string[] { "Medical", "Staging  new string[] { Non-violent}"}},
                {"MEDSU",  new string[] { "Medical", "Medical - Suicide"}},
                {"MEDSZ",  new string[] { "Medical", "Seizures"}},
                {"MEDT",  new string[] { "Medical", "Medical Transfer"}},
                {"MEDTRA",  new string[] { "Medical", "Trauma"}},
                {"MEDU",  new string[] { "Medical", "Medical, Unresponsive"}},
                {"MEDUNR",  new string[] { "Medical", "Unresp / Breathing"}},
                {"MEDUU",  new string[] { "Medical", "Uncon / Unresp"}},
                {"MISC",  new string[] { "Miscellaneous", "Disp. Discretion"}},
                {"MOA",  new string[] { "Medical", "Medical Assist"}},
                {"MOAEMD",  new string[] { "Medical", "Other Assist E M D"}},
                {"MOAT",  new string[] { "Medical", "Assist T/C"}},
                {"MRE",  new string[] { "Medical", "Medical Rescue"}},
                {"MREBC",  new string[] { "Medical", "Building Collapse"}},
                {"MRECLF",  new string[] { "Medical", "Cliff Rescue"}},
                {"MRECS",  new string[] { "Medical", "Conf Space/Trench"}},
                {"MREI",  new string[] { "Medical", "Med Res, Industrial"}},
                {"MRELG",  new string[] { "Medical", "Lifeguard SRF Rescue"}},
                {"MREM",  new string[] { "Medical", "Med Res, Mine"}},
                {"MREO",  new string[] { "Medical", "Med Res, Ocean"}},
                {"MREOTS",  new string[] { "Medical", "Med Res, Over the Side"}},
                {"MRERA",  new string[] { "Medical", "Med Res, Remote Area"}},
                {"MRESH",  new string[] { "Medical", "Med - Res - Short Haul"}},
                {"MRESRF",  new string[] { "Medical", "Surf Rescue"}},
                {"MRESW",  new string[] { "Medical", "Med Res, Static Water"}},
                {"MRESWF",  new string[] { "Medical", "Med Res, Swift Water"}},
                {"MREUSR",  new string[] { "Medical", "USAR"}},
                {"MREW",  new string[] { "Medical", "Water Rescue"}},
                {"MTC",  new string[] { "Medical", "Traffic Collision"}},
                {"MTCA",  new string[] { "Medical", "T/C - Amb Ride-In"}},
                {"MTCF",  new string[] { "Medical", "T/C With Fire"}},
                {"MTCFW",  new string[] { "Medical", "T/C Freeway"}},
                {"MTCH",  new string[] { "Medical", "T/C High Speed"}},
                {"MTCL",  new string[] { "Medical", "T/C Low Speed"}},
                {"MTCM",  new string[] { "Medical", "T/C Multi-Casualty"}},
                {"MTCMV",  new string[] { "Medical", "T/C Multi-Vehicle"}},
                {"MTCOTS",  new string[] { "Medical", "T/C Over the Side"}},
                {"MTCPED",  new string[] { "Medical", "T/C Auto vs. Pedestrian"}},
                {"MTCS",  new string[] { "Medical", "T/C into Structure"}},
                {"MTCU",  new string[] { "Medical", "T/C Unknown Injuries"}},
                {"MTCW",  new string[] { "Medical", "T/C with Injuries"}},
                {"MTX",  new string[] { "Medical", "With Extrication"}},
                {"MTXA",  new string[] { "Medical", "T/X - Amb Ride-In"}},
                {"MVI",  new string[] { "Medical", "Violence Involved"}},
                {"MVIM",  new string[] { "Medical", "MVI, Mass Casualty"}},
                {"MVIS",  new string[] { "Medical", "Stabbing - Shooting"}},
                {"MVISTG",  new string[] { "Medical", "Staging Required"}},
                {"OAC",  new string[] { "Other", "Cover"}},
                {"OACA",  new string[] { "Other", "Cover Ambulance"}},
                {"OACE",  new string[] { "Other", "Cover Engine"}},
                {"OACM",  new string[] { "Other", "Medical Cover"}},
                {"OAF",  new string[] { "Other", "Flight Following"}},
                {"OAFC",  new string[] { "Other", "Flight Follow, CO-OP"}},
                {"OAFM",  new string[] { "Other", "Flight Follow, Med"}},
                {"OAFMHA",  new string[] { "Other", "Med Copter Activity"}},
                {"OAM",  new string[] { "Other", "Miscellaneous"}},
                {"OAMAD",  new string[] { "Other", "Agency Death"}},
                {"OAMADM",  new string[] { "Other", "Other, Administrative"}},
                {"OAMAI",  new string[] { "Other", "Agency Injury"}},
                {"OAMCAD",  new string[] { "Other", "CAD Down"}},
                {"OAMCSM",  new string[] { "Other", "CISD"}},
                {"OAMD",  new string[] { "Other", "Display"}},
                {"OAMDE",  new string[] { "Other", "Damage to Equip/Fac"}},
                {"OAMI",  new string[] { "Other", "MISC Investigation"}},
                {"OAMPHN",  new string[] { "Other", "Phone System Down"}},
                {"OAMRAD",  new string[] { "Other", "Radio Repair"}},
                {"OAMRO",  new string[] { "Other", "Resource Order"}},
                {"OAMSO",  new string[] { "Other", "SO/Medcom Incident"}},
                {"OAMT",  new string[] { "Other", "Misc. Training"}},
                {"OAMT1",  new string[] { "Other", "Training  new string[] { w-Inc #}"}},
                {"OAMTE",  new string[] { "Other", "Theft of Equip"}},
                {"OAMTEC",  new string[] { "Other", "Theft of Equip, CDF"}},
                {"OAMTEL",  new string[] { "Other", "Theft of Equip, Lcl"}},
                {"OAMVA",  new string[] { "Other", "CDF Vehicle Accident"}},
                {"OAP",  new string[] { "Other", "Staffing Pattern"}},
                {"OAR",  new string[] { "Other", "Referral"}},
                {"OARN",  new string[] { "Other", "Referral to NAVDG"}},
                {"OASP",  new string[] { "Other", "Staffing Pattern"}},
                {"OAT",  new string[] { "Other", "Transfer"}},
                {"OAV",  new string[] { "Other", "Veg Mgmt"}},
                {"OES",  new string[] { "Other", "Services"}},
                {"OESA",  new string[] { "Other", "Alarm Test"}},
                {"OESK",  new string[] { "Other", "Knox Box"}},
                {"OESL",  new string[] { "Other", "Local Request"}},
                {"OESL1",  new string[] { "Other", "Local Request new string[] { W/Inc#}},"}},
                {"OEST",  new string[] { "Other", "TRNG Announcement"}},
                {"OOA",  new string[] { "Other", "Assist"}},
                {"OOABP",  new string[] { "Other", "Burn Permit"}},
                {"OOACB",  new string[] { "Other", "Control Burn"}},
                {"OOAFC",  new string[] { "Other", "Fire Crews"}},
                {"OOAHT",  new string[] { "Other", "Helitack Training"}},
                {"OOAME",  new string[] { "Other", "Media"}},
                {"OOAOFS",  new string[] { "Other", "OFS"}},
                {"OOASH",  new string[] { "Other", "Shorthaul Training"}},
                {"OOU",  new string[] { "Other", "Out of Unit"}},
                {"OOUA",  new string[] { "Other", "Out of Unit, Aircrft"}},
                {"OOUC",  new string[] { "Other", "Out of Unit, Crews"}},
                {"OOUE",  new string[] { "Other", "Out of Unit, Equip"}},
                {"OOUED",  new string[] { "Other", "Out of Unit, Eq Doz"}},
                {"OOUEE",  new string[] { "Other", "Out of Unit, Eq Eng"}},
                {"OOUM",  new string[] { "Other", "Out of Unit, MISC"}},
                {"OOUO",  new string[] { "Other", "Out of Unit, Ovrhead"}},
                {"OOUOES",  new string[] { "Other", "Out of Unit OES"}},
                {"OUT",  new string[] { "Other", "Out of Service"}},
                {"PAA",  new string[] { "Public Assist", "Agency"}},
                {"PAD",  new string[] { "Public Assist", "Demo"}},
                {"PAF",  new string[] { "Public Assist", "Flooding"}},
                {"PAO",  new string[] { "Public Assist", "Other"}},
                {"PAO1",  new string[] { "Public Assist", "Other - Engine Only"}},
                {"PAO2",  new string[] { "Public Assist", "Other - Eng and Comp"}},
                {"PAOAN",  new string[] { "Public Assist", "Animal"}},
                {"PAOC",  new string[] { "Public Assist", "Civil Disturb"}},
                {"PAOLG",  new string[] { "Public Assist", "Lifeguard"}},
                {"PAOS",  new string[] { "Public Assist", "Salvage"}},
                {"PAOT",  new string[] { "Public Assist", "Traffic Hazard"}},
                {"PAP",  new string[] { "Public Assist", "Person"}},
                {"PSR",  new string[] { "Public Assist", "Search & Rescue"}}
            };
        }
    }

}