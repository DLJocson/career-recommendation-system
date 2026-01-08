using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Text.Json;

namespace CareerPathRecommender
{
    // DATA MODELS
    
    /// <summary>Career path with scoring based on user responses</summary>
    public class Career
    {
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string SalaryRange { get; set; }
        public int Score { get; set; }
    }

    /// <summary>Assessment question with multiple choice options</summary>
    public class Question
    {
        public required string Text { get; set; }
        public required List<Option> Options { get; set; }
    }

    /// <summary>Answer option with career scoring impacts and optional badge</summary>
    public class Option
    {
        public required string Text { get; set; }
        /// <summary>Maps career names to point adjustments (positive or negative)</summary>
        public required Dictionary<string, int> Impact { get; set; }
        public string? BadgeAwarded { get; set; }
    }

    class Program
    {
        static List<Career> careers = new List<Career>();
        static Dictionary<string, Career> careerLookup = new Dictionary<string, Career>();
        static List<string> earnedBadges = new List<string>();
        static string userName = "Guest";
        static string questionsFilePath = "questions.json";
        static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            InitializeData();

            while (true)
            {
                Console.Clear();
                DrawHeader();
                Console.WriteLine("\n  MAIN MENU");
                Console.WriteLine("  1. Start New Assessment");
                Console.WriteLine("  2. Load Previous Result (Historical Comparison)");
                Console.WriteLine("  3. Admin Mode (God Mode)");
                Console.WriteLine("  4. Exit");
                Console.Write("\n  Select an option: ");
                
                var key = Console.ReadKey(true).Key;
                PlaySound(true);

                switch (key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        RunAssessment();
                        break;
                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        LoadPreviousResult();
                        break;
                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        RunAdminMode();
                        break;
                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        return;
                }
            }
        }

        /// <summary>Runs complete assessment workflow: questions, scoring, tie-breaking, results</summary>
        static void RunAssessment()
        {
            Console.Clear();
            DrawHeader();
            Console.WriteLine("\n  We will analyze your personality, skills, and work style.");
            
            Console.Write("\n  Please enter your name: ");
            string? inputName = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(inputName)) userName = inputName;

            Console.WriteLine($"\n  Hello, {userName}. Press [ENTER] to begin assessment...");
            Console.ReadLine();

            var questions = LoadQuestionsWithFallback();
            int currentQ = 1;
            ResetScores();

            foreach (var q in questions)
            {
                var selectedOption = ShowInteractiveMenu(q, currentQ, questions.Count);
                ApplyScore(selectedOption);
                PlaySound(true);
                ShowLoadingBar("Processing...", 15); 
                currentQ++;
            }

            var sortedCareers = careers.OrderByDescending(c => c.Score).ToList();
            
            // Trigger tie-breaker if top 2 careers within 5 points
            if (sortedCareers.Count >= 2 && sortedCareers[0].Score - sortedCareers[1].Score < 5)
            {
                RunTieBreaker(sortedCareers[0], sortedCareers[1]);
                sortedCareers = careers.OrderByDescending(c => c.Score).ToList();
            }

            var topCareer = sortedCareers[0];
            DisplayDashboard(sortedCareers, topCareer);
            HandlePostAssessment(sortedCareers, topCareer);
        }

        static void ResetScores()
        {
            foreach(var c in careers) c.Score = 0;
            earnedBadges.Clear();
        }

        /// <summary>Loads questions from JSON file, falls back to defaults if file missing or invalid</summary>
        static List<Question> LoadQuestionsWithFallback()
        {
            if (File.Exists(questionsFilePath))
            {
                try
                {
                    string jsonString = File.ReadAllText(questionsFilePath);
                    var loadedQuestions = JsonSerializer.Deserialize<List<Question>>(jsonString);
                    
                    if (loadedQuestions != null && loadedQuestions.Count > 0)
                        return loadedQuestions;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [!] Error loading JSON: {ex.Message}. Using defaults.");
                }
            }

            var defaults = GetDefaultQuestions();
            try
            {
                string jsonString = JsonSerializer.Serialize(defaults, jsonOptions);
                File.WriteAllText(questionsFilePath, jsonString);
            }
            catch { /* Ignore write errors */ }

            return defaults;
        }

        /// <summary>Tie-breaker question when top 2 careers within 5 points. Winner gets +10, loser -5</summary>
        static void RunTieBreaker(Career c1, Career c2)
        {
            PlaySound(false);
            
            Console.Clear();
            DrawHeader();
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  ⚠ TIE DETECTED! SUDDEN DEATH ROUND ⚠");
            Console.ResetColor();
            Console.WriteLine($"  It is too close to call between {c1.Name} and {c2.Name}.");
            
            var q = new Question
            {
                Text = $"If you had to choose one path right now, which appeals more?",
                Options = new List<Option>
                {
                    new Option { Text = $"Focus on {c1.Name} tasks", Impact = new Dictionary<string, int> { { c1.Name, 10 }, { c2.Name, -5 } } },
                    new Option { Text = $"Focus on {c2.Name} tasks", Impact = new Dictionary<string, int> { { c2.Name, 10 }, { c1.Name, -5 } } }
                }
            };

            var decision = ShowInteractiveMenu(q, 99, 99);
            ApplyScore(decision);
        }

        /// <summary>Admin mode for testing - view stats and manually adjust scores (password: 1234)</summary>
        static void RunAdminMode()
        {
            Console.Clear();
            DrawHeader();
            Console.Write("\n  Enter Admin Password: ");
            string? pass = Console.ReadLine();
            
            if (pass != "1234")
            {
                PlaySound(false);
                Console.WriteLine("  Access Denied.");
                Thread.Sleep(1000);
                return;
            }

            PlaySound(true);
            
            while (true)
            {
                Console.Clear();
                
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  *** ADMIN / GOD MODE ***");
                Console.ResetColor();
                
                Console.WriteLine("  1. View Raw Career Stats");
                Console.WriteLine("  2. Add Bonus Points to a Career");
                Console.WriteLine("  3. Return to Main Menu");
                
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.D3) return;

                if (key == ConsoleKey.D1)
                {
                    foreach(var c in careers) Console.WriteLine($"  - {c.Name}: {c.Score} pts");
                    Console.ReadKey();
                }
                
                if (key == ConsoleKey.D2)
                {
                    Console.WriteLine("\n  Add 50 pts to Software Developer? (Y/N)");
                    if (Console.ReadKey(true).Key == ConsoleKey.Y)
                    {
                        if (careerLookup.TryGetValue("Software Developer", out var dev))
                        {
                            dev.Score += 50;
                            Console.WriteLine("  Updated.");
                            Thread.Sleep(500);
                        }
                    }
                }
            }
        }

        /// <summary>Loads and displays saved assessment results from CareerPath_*.txt files</summary>
        static void LoadPreviousResult()
        {
            Console.Clear();
            DrawHeader();
            
            string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "CareerPath_*.txt");
            
            if (files.Length == 0)
            {
                Console.WriteLine("\n  No history found.");
                Console.WriteLine("  Press any key to return.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\n  Select a file to load:");
            for(int i=0; i<files.Length; i++)
            {
                Console.WriteLine($"  {i+1}. {Path.GetFileName(files[i])}");
            }

            Console.Write("\n  Choice: ");
            if(int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= files.Length)
            {
                string content = File.ReadAllText(files[choice-1]);
                Console.Clear();
                DrawHeader();
                Console.WriteLine("\n  HISTORICAL REPORT");
                Console.WriteLine("  -----------------");
                Console.WriteLine(content);
                Console.WriteLine("\n  Press any key to return.");
                Console.ReadKey();
            }
        }

        static void HandlePostAssessment(List<Career> sorted, Career top)
        {
            Console.WriteLine("\n  ACTIONS:");
            Console.WriteLine("  [S] Save to File");
            Console.WriteLine("  [E] Email Results");
            Console.WriteLine("  [ENTER] Return to Menu");
            
            var key = Console.ReadKey(true).Key;
            
            if (key == ConsoleKey.S)
            {
                SaveResults(sorted, top);
            }
            else if (key == ConsoleKey.E)
            {
                EmailResults(sorted, top);
            }
        }

        /// <summary>Email simulation - set simulation=false and configure SMTP for actual sending</summary>
        static void EmailResults(List<Career> sorted, Career top)
        {
            Console.Write("\n  Enter your email address: ");
            string? email = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                Console.WriteLine("  Invalid email.");
                return;
            }

            Console.WriteLine($"  Sending report to {email}...");
            ShowLoadingBar("Connecting to SMTP", 20);

            // Set to false to enable actual email sending (requires SMTP configuration below)
            bool simulation = false;

            if (simulation)
            {
                Console.WriteLine("  [SIMULATION] Email sent successfully!");
            }
            else
            {
                try
                {
                    // IMPORTANT: Configure these SMTP settings before use:
                    // 1. Replace "your_app@example.com" with your sender email
                    // 2. Replace "username" and "password" with actual credentials
                    // 3. For Gmail: Enable "App Passwords" in Google Account settings
                    //    (2-factor auth must be enabled first)
                    // 4. Use the 16-character App Password instead of your regular password
                    
                    MailMessage mail = new MailMessage();
                    SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                    mail.From = new MailAddress("your_app@example.com");
                    mail.To.Add(email);
                    mail.Subject = "Your Career Path Results";
                    mail.Body = GenerateReportString(sorted, top);
                    SmtpServer.Port = 587;
                    SmtpServer.Credentials = new NetworkCredential("username", "password");
                    SmtpServer.EnableSsl = true;
                    SmtpServer.Send(mail);
                    
                    Console.WriteLine("  ✓ Email sent successfully!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  ✗ Error sending email: " + ex.Message);
                    Console.WriteLine("  Make sure SMTP credentials are configured correctly.");
                }
            }
            Thread.Sleep(1000);
        }

        /// <summary>System beep feedback - high pitch for success, low for alert (Windows only)</summary>
        static void PlaySound(bool success)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    if (success) 
                        Console.Beep(1000, 100);
                    else 
                        Console.Beep(200, 300);
                }
            }
            catch { /* Ignore audio errors */ }
        }

        static void InitializeData()
        {
            careers.Clear();
            careerLookup.Clear();
            
            var softwareDev = new Career 
            { 
                Name = "Software Developer", 
                SalaryRange = "₱30k - ₱150k per month",
                Description = "You build the systems that run the world. You love logic, problem-solving, and seeing code come to life. High demand in BGC, Makati, and offshore roles.",
                Score = 0 
            };
            careers.Add(softwareDev);
            careerLookup[softwareDev.Name] = softwareDev;
            
            var uiuxDesigner = new Career 
            { 
                Name = "UI/UX Designer", 
                SalaryRange = "₱25k - ₱100k per month",
                Description = "You bridge the gap between human and machine. You care about aesthetics, user empathy, and intuitive flows. Growing demand in startups and tech companies.",
                Score = 0 
            };
            careers.Add(uiuxDesigner);
            careerLookup[uiuxDesigner.Name] = uiuxDesigner;
            
            var dataAnalyst = new Career 
            { 
                Name = "Data Analyst", 
                SalaryRange = "₱25k - ₱90k per month",
                Description = "You turn noise into knowledge. You love patterns, statistics, and finding the truth hidden in spreadsheets. Essential in BPO, finance, and e-commerce sectors.",
                Score = 0 
            };
            careers.Add(dataAnalyst);
            careerLookup[dataAnalyst.Name] = dataAnalyst;
            
            var cyberSecurity = new Career 
            { 
                Name = "Cybersecurity Analyst", 
                SalaryRange = "₱35k - ₱120k per month",
                Description = "The digital guardian. You enjoy breaking things to fix them, analyzing threats, and protecting systems. Critical role in banking, government, and enterprise IT.",
                Score = 0 
            };
            careers.Add(cyberSecurity);
            careerLookup[cyberSecurity.Name] = cyberSecurity;
        }

        static List<Question> GetDefaultQuestions()
        {
            return new List<Question>
            {
                new Question
                {
                    Text = "How do you feel about advanced mathematics?",
                    Options = new List<Option>
                    {
                        new Option { Text = "I love it.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 }, { "Software Developer", 5 } } },
                        new Option { Text = "It's okay if necessary.", Impact = new Dictionary<string, int> { { "Software Developer", 2 } } },
                        new Option { Text = "I hate it.", Impact = new Dictionary<string, int> { { "Data Analyst", -10 }, { "UI/UX Designer", 5 } } },
                    }
                },
                new Question
                {
                    Text = "Which activity sounds most appealing for a Saturday afternoon?",
                    Options = new List<Option>
                    {
                        new Option { Text = "Solving a complex logic puzzle or riddle.", Impact = new Dictionary<string, int> { { "Software Developer", 10 }, { "Cybersecurity Analyst", 5 } }, BadgeAwarded = "Puzzle Master" },
                        new Option { Text = "Sketching, painting, or redecorating your room.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } }, BadgeAwarded = "Creative Soul" },
                        new Option { Text = "Organizing your budget or categorizing a collection.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } }, BadgeAwarded = "Organizer" },
                        new Option { Text = "Learning how a lock works or watching crime docs.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 }, { "Software Developer", 3 } }, BadgeAwarded = "Investigator" }
                    }
                },
                new Question
                {
                    Text = "In a group project, what role do you usually take?",
                    Options = new List<Option>
                    {
                        new Option { Text = "The one who makes the slides look amazing.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 8 }, { "Software Developer", 2 } } },
                        new Option { Text = "The builder who actually puts the project together.", Impact = new Dictionary<string, int> { { "Software Developer", 10 }, { "Cybersecurity Analyst", 4 } } },
                        new Option { Text = "The researcher who checks facts and finds trends.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 }, { "Cybersecurity Analyst", 5 } } }
                    }
                },
                new Question
                {
                    Text = "How do you prefer to solve problems?",
                    Options = new List<Option>
                    {
                        new Option { Text = "Visualizing the end result first.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Breaking it down into small, logical steps.", Impact = new Dictionary<string, int> { { "Software Developer", 10 }, { "Data Analyst", 5 } } },
                        new Option { Text = "Looking for vulnerabilities or weak points.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } },
                        new Option { Text = "Looking at historical data to predict the outcome.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } }
                    }
                },
                new Question
                {
                    Text = "Pick a tool you'd rather learn:",
                    Options = new List<Option>
                    {
                        new Option { Text = "Photoshop / Figma", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Visual Studio / VS Code", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "Excel / PowerBI", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } },
                        new Option { Text = "Kali Linux / Wireshark", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } }
                    }
                },
                new Question
                {
                    Text = "A website feature isn't working. What is your first instinct?",
                    Options = new List<Option>
                    {
                        new Option { Text = "Check the code for syntax errors.", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "Check if the layout is broken on mobile.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Check if the database connection failed.", Impact = new Dictionary<string, int> { { "Data Analyst", 8 }, { "Software Developer", 5 } } },
                        new Option { Text = "Check if it was a malicious attack.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } }, BadgeAwarded = "Paranoid (Good)" }
                    }
                },
                new Question
                {
                    Text = "You get a huge box of LEGOs. What do you do?",
                    Options = new List<Option>
                    {
                        new Option { Text = "Sort them by color and size first.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } }, BadgeAwarded = "Sorter" },
                        new Option { Text = "Build a strong, fortified castle.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 8 }, { "Software Developer", 5 } } },
                        new Option { Text = "Build something that looks beautiful on a shelf.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Build a working robot or machine.", Impact = new Dictionary<string, int> { { "Software Developer", 10 } }, BadgeAwarded = "Engineer" }
                    }
                },
                new Question
                {
                    Text = "What kind of feedback hurts you the most?",
                    Options = new List<Option>
                    {
                        new Option { Text = "'This looks ugly.'", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "'This doesn't work correctly.'", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "'This data is inaccurate.'", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } },
                        new Option { Text = "'This isn't secure.'", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } }
                    }
                },
                new Question
                {
                    Text = "Choose a superpower:",
                    Options = new List<Option>
                    {
                        new Option { Text = "Invisibility (To go unseen).", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } },
                        new Option { Text = "Creation (To make things from nothing).", Impact = new Dictionary<string, int> { { "Software Developer", 8 }, { "UI/UX Designer", 8 } } },
                        new Option { Text = "Omniscience (To know all facts).", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } }, BadgeAwarded = "All-Knowing" },
                        new Option { Text = "Telepathy (To understand what people want).", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } }
                    }
                },
                new Question
                {
                    Text = "If you worked at a bank, where would you be?",
                    Options = new List<Option>
                    {
                        new Option { Text = "Designing the mobile app interface.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Building the transaction processing engine.", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "Analyzing spending trends for reports.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } },
                        new Option { Text = "Securing the vault and firewalls.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } }, BadgeAwarded = "Guardian" }
                    }
                },
                new Question
                {
                    Text = "What is your preferred browser tab situation?",
                    Options = new List<Option>
                    {
                        new Option { Text = "50+ tabs, open to Stack Overflow.", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "Neatly organized bookmark folders.", Impact = new Dictionary<string, int> { { "Data Analyst", 8 }, { "UI/UX Designer", 5 } } },
                        new Option { Text = "Incognito / Private mode mostly.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } },
                        new Option { Text = "Tabs for Pinterest, Dribbble, Behance.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } }
                    }
                },
                new Question
                {
                    Text = "You find a USB drive in the office parking lot. You:",
                    Options = new List<Option>
                    {
                        new Option { Text = "Plug it in to see what's on it.", Impact = new Dictionary<string, int> { { "Software Developer", 5 }, { "UI/UX Designer", 5 } } },
                        new Option { Text = "Never plug it in! It's a trap.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 12 } }, BadgeAwarded = "Security Conscious" },
                        new Option { Text = "Plug it in on an isolated, air-gapped machine.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 8 }, { "Data Analyst", 5 } } },
                        new Option { Text = "Leave it alone.", Impact = new Dictionary<string, int> { { "Data Analyst", 2 }, { "UI/UX Designer", 2 } } }
                    }
                },
                new Question
                {
                    Text = "Which movie character role appeals to you?",
                    Options = new List<Option>
                    {
                        new Option { Text = "The Architect (The Matrix) - Designing the system.", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "Iron Man - Building cool visual interfaces.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Sherlock Holmes - Deducing facts from data.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } },
                        new Option { Text = "Mr. Robot - Hacking and exposing secrets.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } }
                    }
                },
                new Question
                {
                    Text = "What aspect of video games do you appreciate most?",
                    Options = new List<Option>
                    {
                        new Option { Text = "The graphics and art style.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "The game mechanics and physics.", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "The stats, DPS charts, and loot tables.", Impact = new Dictionary<string, int> { { "Data Analyst", 12 } }, BadgeAwarded = "Min-Maxer" },
                        new Option { Text = "Finding glitches or cheats.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } }
                    }
                },
                new Question
                {
                    Text = "You have to present a project. How do you prepare?",
                    Options = new List<Option>
                    {
                        new Option { Text = "Make beautiful, interactive slides.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Prepare charts, graphs, and statistics.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } },
                        new Option { Text = "Do a live demo of the functionality.", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "Explain the risk assessment and safety protocols.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } }
                    }
                },
                new Question
                {
                    Text = "Preferred work environment?",
                    Options = new List<Option>
                    {
                        new Option { Text = "A creative studio with mood boards (like in BGC).", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Quiet room, headphones on, multiple monitors.", Impact = new Dictionary<string, int> { { "Software Developer", 8 }, { "Data Analyst", 8 } } },
                        new Option { Text = "A command center watching live network traffic.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } },
                        new Option { Text = "Collaborative open space (hybrid setup is fine).", Impact = new Dictionary<string, int> { { "UI/UX Designer", 5 }, { "Software Developer", 5 } } }
                    }
                },
                new Question
                {
                    Text = "Which phrase annoys you the most?",
                    Options = new List<Option>
                    {
                        new Option { Text = "\"Can you make the logo bigger?\"", Impact = new Dictionary<string, int> { { "UI/UX Designer", 12 } } },
                        new Option { Text = "\"It works on my machine.\"", Impact = new Dictionary<string, int> { { "Software Developer", 12 } } },
                        new Option { Text = "\"We don't need a password for that.\"", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 12 } } },
                        new Option { Text = "\"Just guess the numbers for now.\"", Impact = new Dictionary<string, int> { { "Data Analyst", 12 } } }
                    }
                },
                new Question
                {
                    Text = "What would you automate first?",
                    Options = new List<Option>
                    {
                        new Option { Text = "My daily emails and file sorting.", Impact = new Dictionary<string, int> { { "Software Developer", 8 }, { "Data Analyst", 5 } } },
                        new Option { Text = "Security scans of my home network.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } },
                        new Option { Text = "Creating consistent color palettes.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Collecting PSE (Philippine Stock Exchange) data.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } }
                    }
                },
                new Question
                {
                    Text = "Pick a geometric shape.",
                    Options = new List<Option>
                    {
                        new Option { Text = "A perfect Circle (Harmony).", Impact = new Dictionary<string, int> { { "UI/UX Designer", 8 } } },
                        new Option { Text = "A Square (Structure/Logic).", Impact = new Dictionary<string, int> { { "Software Developer", 8 } } },
                        new Option { Text = "A Grid (Organization).", Impact = new Dictionary<string, int> { { "Data Analyst", 8 } } },
                        new Option { Text = "A Shield (Protection).", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 8 } } }
                    }
                },
                new Question
                {
                    Text = "If you were a writer, what would you write?",
                    Options = new List<Option>
                    {
                        new Option { Text = "A technical instruction manual.", Impact = new Dictionary<string, int> { { "Software Developer", 10 } } },
                        new Option { Text = "A mystery novel.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 8 }, { "Data Analyst", 5 } } },
                        new Option { Text = "A graphic novel / comic.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "An encyclopedia.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } }
                    }
                },
                new Question
                {
                    Text = "Final Question: What drives you?",
                    Options = new List<Option>
                    {
                        new Option { Text = "Making things that people enjoy using.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 }, { "Software Developer", 5 } } },
                        new Option { Text = "Finding the objective truth.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } },
                        new Option { Text = "Protecting people from harm.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } },
                        new Option { Text = "Solving difficult technical challenges.", Impact = new Dictionary<string, int> { { "Software Developer", 10 }, { "Cybersecurity Analyst", 5 } } }
                    }
                }
            };
        }

        /// <summary>Applies option's score impacts and awards badge if not already earned</summary>
        static void ApplyScore(Option option)
        {
            foreach (var impact in option.Impact)
            {
                if (careerLookup.TryGetValue(impact.Key, out var career))
                {
                    career.Score += impact.Value;
                }
            }

            if (!string.IsNullOrEmpty(option.BadgeAwarded))
            {
                if (!earnedBadges.Contains(option.BadgeAwarded))
                    earnedBadges.Add(option.BadgeAwarded);
            }
        }

        /// <summary>Interactive arrow-key menu with UP/DOWN navigation and ENTER to select</summary>
        static Option ShowInteractiveMenu(Question q, int current, int total)
        {
            int index = 0;
            ConsoleKey key;

            do
            {
                Console.Clear();
                DrawHeader();
                Console.WriteLine($"\n  QUESTION {current} of {total}: {q.Text}");
                Console.WriteLine("  (Use UP/DOWN Arrows to navigate, ENTER to select)\n");

                for (int i = 0; i < q.Options.Count; i++)
                {
                    if (i == index)
                    {
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.BackgroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"  > {q.Options[i].Text}  ");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"    {q.Options[i].Text}");
                    }
                }

                key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.UpArrow)
                {
                    index--;
                    if (index < 0) index = q.Options.Count - 1;
                }
                else if (key == ConsoleKey.DownArrow)
                {
                    index++;
                    if (index >= q.Options.Count) index = 0;
                }

            } while (key != ConsoleKey.Enter);

            return q.Options[index];
        }

        static void DrawHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine("       CAREER PATH DECISION SYSTEM v3.0           ");
            Console.WriteLine("==================================================");
            Console.ResetColor();
        }

        static void ShowLoadingBar(string message, int speed = 30)
        {
            Console.Write($"\n  {message} [");
            
            for (int i = 0; i < 20; i++)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("█");
                Console.ResetColor();
                Thread.Sleep(speed);
            }
            
            Console.WriteLine("] Done.");
            Thread.Sleep(200);
        }

        static void DisplayDashboard(List<Career> sortedCareers, Career topCareer)
        {
            Console.Clear();
            DrawHeader();
            Console.WriteLine($"\n  ASSESSMENT COMPLETE FOR: {userName.ToUpper()}");
            Console.WriteLine("  --------------------------------------------------");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ★ TOP RECOMMENDATION: {topCareer.Name.ToUpper()} ★");
            Console.ResetColor();
            Console.WriteLine($"  Salary Range: {topCareer.SalaryRange}");
            Console.WriteLine($"  {WrapText(topCareer.Description, 60)}");

            Console.WriteLine("\n  MATCH ANALYSIS");
            Console.WriteLine("  --------------------------------------------------");
            foreach (var career in sortedCareers)
            {
                DrawBarChart(career.Name, career.Score, 50);
            }

            if (earnedBadges.Count > 0)
            {
                Console.WriteLine("\n  EARNED BADGES");
                Console.WriteLine("  --------------------------------------------------");
                
                var badgeDisplay = new System.Text.StringBuilder("  ");
                foreach (var badge in earnedBadges)
                {
                    badgeDisplay.Append($"[{badge}] ");
                }
                Console.WriteLine(badgeDisplay.ToString().TrimEnd());
            }
            
            Console.WriteLine("\n  Thank you for using the Career Path Decision System!");
            Console.WriteLine("  Press any key to return to the main menu.");
            Console.ReadKey();
        }

        /// <summary>Draws bar chart scaled to maxWidth (score normalized to 130 max)</summary>
        static void DrawBarChart(string careerName, int score, int maxWidth)
        {
            int scaledWidth = (int)((double)score / 130 * maxWidth);
            
            if (scaledWidth < 0) scaledWidth = 0;
            if (scaledWidth > maxWidth) scaledWidth = maxWidth;
            
            Console.Write($"    {careerName}: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(new string('█', scaledWidth) + new string('─', maxWidth - scaledWidth));
            Console.ResetColor();
        }

        /// <summary>Wraps text at word boundaries to specified line width</summary>
        static string WrapText(string text, int lineWidth)
        {
            var sb = new System.Text.StringBuilder();
            int currentLineLength = 0;
            
            foreach (var word in text.Split(' '))
            {
                if (currentLineLength + word.Length + 1 > lineWidth)
                {
                    sb.AppendLine();
                    currentLineLength = 0;
                }
                else if (currentLineLength > 0)
                {
                    sb.Append(' ');
                    currentLineLength++;
                }
                
                sb.Append(word);
                currentLineLength += word.Length;
            }
            
            return sb.ToString();
        }
        
        static string GenerateReportString(List<Career> sorted, Career top)
        {
            var sb = new StringBuilder(512);
            
            sb.AppendLine("CAREER PATH REPORT");
            sb.Append("User: ").AppendLine(userName);
            sb.Append("Date: ").AppendLine(DateTime.Now.ToString());
            sb.AppendLine("--------------------------------");
            
            sb.Append("Top Recommendation: ").AppendLine(top.Name);
            sb.Append("Potential Salary: ").AppendLine(top.SalaryRange);
            sb.AppendLine("--------------------------------");
            
            sb.AppendLine("Full Breakdown:");
            foreach (var c in sorted)
            {
                sb.Append("- ").Append(c.Name).Append(": ").Append(c.Score).AppendLine(" points");
            }
            sb.AppendLine("--------------------------------");
            
            sb.AppendLine("Badges Earned:");
            foreach(var b in earnedBadges) 
            {
                sb.Append("* ").AppendLine(b);
            }
            
            return sb.ToString();
        }

        static void SaveResults(List<Career> sorted, Career top)
        {
            string fileName = $"CareerPath_{userName}_{DateTime.Now:yyyyMMdd}.txt";
            string content = GenerateReportString(sorted, top);
            File.WriteAllText(fileName, content);
            
            Console.WriteLine($"\n  [SUCCESS] Report saved to {fileName}");
            Console.WriteLine("  Press any key to return.");
            Console.ReadKey();
        }
    }
}