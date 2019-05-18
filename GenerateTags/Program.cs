using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

namespace GenerateTags
{
    public class Program
    {

        enum WeekDays
        {
            Monday = 0, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
        }

        public class Food
        {
            public string MainDish;
            public string SideDish = null;
            public bool IsHot = false;
        }

        static Dictionary<WeekDays, List<Food>> WeekMenu = new Dictionary<WeekDays, List<Food>>();
        static Dictionary<WeekDays, Dictionary<string, List<Food>>> PersonLocationChoices = new Dictionary<WeekDays, Dictionary<string, List<Food>>>();
        static List<string> Locations = new List<string>();

        const string WEEKLY_MENU_FILENAME = "WeeklyMenu.csv";
        const string WEEKLY_MENU_SIDE_FILENAME = "WeeklyMenuSide.csv";
        const string ORDERS_FILENAME = "Orders.csv";
        static string WORK_PATH;
        static int COLUMNS_HOT = 4;
        static int COLUMNS_COLD = 5;


        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine();
            Console.WriteLine("Tag Generator : Version 0.1 Feb 2019 - Food & Style (C)");
            Console.WriteLine();

            LoadConfiguration();

            try
            {
                using (StreamReader streamReader = new StreamReader(GetFilePath(WEEKLY_MENU_FILENAME)))
                {
                    string line;
                    int counter = 0;
                    WeekDays currentDay = WeekDays.Monday;

                    WeekMenu.Add(currentDay, new List<Food>());

                    while ((line = streamReader.ReadLine()) != null)
                    {
                        if (counter == 8)
                        {
                            if (currentDay == WeekDays.Sunday)
                                break;
                            currentDay = (WeekDays)((int)currentDay + 1);
                            counter = 0;
                            WeekMenu.Add(currentDay, new List<Food>());
                        }
                        WeekMenu[currentDay].Add(new Food() { MainDish = line.Substring(0, line.IndexOf('/')).Trim(), IsHot = counter < 3 });
                        counter++;
                    }
                }
                if (WeekMenu.Count() != 7)
                {
                    Console.WriteLine($"ERROR: {WEEKLY_MENU_FILENAME} does not contain 56 rows!");
                    Environment.Exit(3);
                }

                List<string> sideDishes = new List<string>();
                using (StreamReader streamReader = new StreamReader(GetFilePath(WEEKLY_MENU_SIDE_FILENAME)))
                {
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        sideDishes.Add(line.Split('/')[0].Trim());
                    }
                }

                foreach (WeekDays day in WeekMenu.Keys)
                {
                    if (sideDishes.Count <= (int)day)
                        break;

                    int count = 0;
                    foreach (Food food in WeekMenu[day])
                    {
                        food.SideDish = sideDishes[(int)day];
                        // Only the first 5 options have a side dish!
                        if (++count == 5)
                            break;
                    }
                }

                //Print Menu in console...
                Console.WriteLine(" === Week Menu === ");
                Console.WriteLine();
                foreach (WeekDays day in WeekMenu.Keys)
                {
                    Console.WriteLine($"{day.ToString()}:");
                    foreach (Food food in WeekMenu[day])
                    {
                        string isHot = food.IsHot ? "(Hot) " : string.Empty;
                        string sideOrder = food.SideDish == null ? "" : $" + {food.SideDish}";
                        Console.WriteLine($" - {isHot}{food.MainDish}{sideOrder}");
                    }
                    Console.WriteLine();
                }


                using (StreamReader orderFile = new StreamReader(GetFilePath(ORDERS_FILENAME)))
                {
                    WeekDays currentDay = WeekDays.Monday;
                    string currentPosition = string.Empty;
                    string line;

                    foreach (WeekDays day in WeekMenu.Keys)
                        PersonLocationChoices.Add(day, new Dictionary<string, List<Food>>());

                    while ((line = orderFile.ReadLine()) != null)
                    {
                        List<string> personData = new List<string>(line.Split(';'));
                        currentPosition = string.IsNullOrWhiteSpace(personData[0]) ? currentPosition : personData[0].Trim();
                        string personLocation = personData[1].Trim() + "@" + currentPosition;

                        // we only use this list for reporting reasons!
                        if (!Locations.Contains(currentPosition))
                            Locations.Add(currentPosition);

                        // user data must be intervals of 8 + 2! Only parse group of 8s
                        int maxIndex = ((personData.Count() - 2) / 8) * 8 + 2;

                        for (int index = 2; index < maxIndex; index++)
                        {
                            currentDay = (WeekDays)((index - 2) / 8);
                            if (!PersonLocationChoices[currentDay].ContainsKey(personLocation))
                                PersonLocationChoices[currentDay].Add(personLocation, new List<Food>());

                            if (string.IsNullOrWhiteSpace(personData[index]) || personData[index] != "1")
                                continue;

                            PersonLocationChoices[currentDay][personLocation].Add(WeekMenu[currentDay][(index - 2) % 8]);
                        }
                    }
                }

                // This section is only for reporting!
                int personLocationCounter = PersonLocationChoices[WeekDays.Monday].Keys.Count();
                Console.WriteLine($"Found {personLocationCounter} unique person-locations.");
                Console.WriteLine();

                int totalServingsCounter = 0;
                int totalSideDishesCounter = 0;
                int servingsPerLocation = 0;
                int sideDishesPerLocation = 0;
                string reportLine;
                foreach (WeekDays day in WeekMenu.Keys)
                {
                    int dailyServingsCounter = 0;
                    int dailySideDishesCounter = 0;
                    foreach (string personLocation in PersonLocationChoices[day].Keys)
                    {
                        dailyServingsCounter += PersonLocationChoices[day][personLocation].Count();
                        dailySideDishesCounter += PersonLocationChoices[day][personLocation].Where(f => f.SideDish != null).Count();
                    }
                    if (dailyServingsCounter == 0)
                        continue;
                    reportLine = $"Found {dailyServingsCounter} servings";
                    if (dailySideDishesCounter > 0)
                        reportLine += $" (+ {dailySideDishesCounter} sides)";
                    Console.WriteLine(reportLine + $" for {day}.");

                    totalServingsCounter += dailyServingsCounter;
                    totalSideDishesCounter += dailySideDishesCounter;

                    foreach (string location in Locations)
                    {
                        servingsPerLocation = 0;
                        sideDishesPerLocation = 0;
                        foreach (string personLocation in PersonLocationChoices[day].Keys.Where(pl => pl.EndsWith(location)))
                        {
                            servingsPerLocation += PersonLocationChoices[day][personLocation].Count();
                            sideDishesPerLocation += PersonLocationChoices[day][personLocation].Where(f => f.SideDish != null).Count();
                        }
                        if (servingsPerLocation == 0)
                            continue;
                        reportLine = $"\t{servingsPerLocation}";
                        if (sideDishesPerLocation > 0)
                            reportLine += $" (+ {sideDishesPerLocation})";
                        Console.WriteLine(reportLine + $" at { location}.");
                    }
                }
                Console.WriteLine();
                reportLine = $"Found {totalServingsCounter} total servings";
                if (totalSideDishesCounter > 0)
                    reportLine += $" (+ {totalSideDishesCounter} sides)";
                Console.WriteLine(reportLine);

                foreach (WeekDays day in WeekMenu.Keys)
                {
                    CreateOrderFile(day, OrderFileType.Hot);
                    CreateOrderFile(day, OrderFileType.Cold);
                    CreateOrderFile(day, OrderFileType.Side);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR : Failed");
                Console.WriteLine($"  Message: {ex.Message ?? string.Empty}");
                Console.WriteLine($"  Stack Trace: {ex.StackTrace ?? string.Empty}");

                if (ex.InnerException == null)
                    return;

                Console.WriteLine($"   Inner Exception Message: {ex.InnerException.Message ?? string.Empty}");
                Console.WriteLine($"   Inner Exception Stack Trace: {ex.InnerException.StackTrace ?? string.Empty}");
            }
        }

        private static string GetFilePath(string fileName)
        {
            string filePath = Path.Combine(WORK_PATH, fileName);
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"ERROR: {fileName} file not found on {WORK_PATH}");
                Environment.Exit(2);
            }
            return filePath;
        }

        private static void LoadConfiguration()
        {
            WORK_PATH = ConfigurationManager.AppSettings["WorkingPath"];
            if (string.IsNullOrWhiteSpace(WORK_PATH))
            {
                Console.WriteLine("ERROR: WorkingPath paramater not set!");
                Environment.Exit(1);
            }

            if (!Directory.Exists(WORK_PATH))
            {
                Console.WriteLine("ERROR: WorkingPath does not exist!");
                Environment.Exit(1);
            }

            COLUMNS_HOT = Int32.Parse(ConfigurationManager.AppSettings["ColumnsHot"] ?? COLUMNS_HOT.ToString());
            COLUMNS_COLD = Int32.Parse(ConfigurationManager.AppSettings["ColumnsCold"] ?? COLUMNS_COLD.ToString());
        }

        enum OrderFileType
        {
            Hot,
            Cold,
            Side
        }

        static void CreateOrderFile(WeekDays day, OrderFileType orderFileType)
        {
            string dayOrderFilePath = Path.Combine(WORK_PATH, $"{day.ToString()}_{orderFileType.ToString()}.html");

            int numberOfColumns = COLUMNS_COLD;
            List<string> dayOrders = new List<string>
            {
                "<html><link rel='stylesheet' type='text/css' href='pageCold.css'/><body><table cellpadding='0' cellspacing='0' >"
            };

            if (orderFileType == OrderFileType.Hot)
            {
                numberOfColumns = COLUMNS_HOT;
                dayOrders = new List<string>
                {
                    "<html><link rel='stylesheet' type='text/css' href='pageHot.css'/><body><table cellpadding='0' cellspacing='0' >"
                };
            }
            
            int columnCounter = 0;

            foreach (Food foodChoice in WeekMenu[day])
            {
                foreach (string personLocation in PersonLocationChoices[day].Keys)
                {
                    string personName = personLocation.Split('@')[0];
                    string location = personLocation.Split('@')[1];

                    IEnumerable<Food> choices;
                    if (orderFileType == OrderFileType.Hot)
                        choices = PersonLocationChoices[day][personLocation].Where(f => f.MainDish == foodChoice.MainDish && f.IsHot == true);
                    else if (orderFileType == OrderFileType.Cold)
                        choices = PersonLocationChoices[day][personLocation].Where(f => f.MainDish == foodChoice.MainDish && f.IsHot == false);
                    else
                        choices = PersonLocationChoices[day][personLocation].Where(f => f.MainDish == foodChoice.MainDish && f.SideDish != null);

                    foreach (Food food in choices)
                    {
                        if (columnCounter % numberOfColumns == 0)
                            dayOrders.Add("<tr>");

                        if (orderFileType == OrderFileType.Side)
                            dayOrders.Add($"<td><div class='tag''><div class='foodName'>{food.SideDish}</div><div class='personName'>{personName}</div><div class='location'>{location}</div></div></td>");
                        else
                            dayOrders.Add($"<td><div class='tag''><div class='foodName'>{food.MainDish}</div><div class='personName'>{personName}</div><div class='location'>{location}</div></div></td>");

                        if (columnCounter % numberOfColumns == numberOfColumns-1)
                            dayOrders.Add("</tr>");

                        columnCounter++;
                    }
                }
            }

            if (columnCounter > 0 && columnCounter % numberOfColumns != numberOfColumns - 1)
                dayOrders.Add("</tr>");

            dayOrders.Add("</table></body></html>");
            
            if (columnCounter > 0)
                File.WriteAllLines(dayOrderFilePath, dayOrders);
        }
    }
}
