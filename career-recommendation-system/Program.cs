using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Text.Json; // Requires .NET Core 3.0+ or .NET 5+

namespace CareerPathRecommender
{
    // ==========================================
    // DATA MODELS
    // ==========================================

    /// <summary>
    /// Represents a career path option with associated metadata and scoring.
    /// Each career accumulates points during the assessment based on user responses.
    /// </summary>
    public class Career
    {
        /// <summary>The name of the career (e.g., "Software Developer")</summary>
        public required string Name { get; set; }
        
        /// <summary>A brief description of what this career entails</summary>
        public required string Description { get; set; }
        
        /// <summary>Expected salary range for this career path</summary>
        public required string SalaryRange { get; set; }
        
        /// <summary>Accumulated score based on user's responses to questions</summary>
        public int Score { get; set; }
    }

    /// <summary>
    /// Represents a single assessment question with multiple choice options.
    /// Questions are loaded from JSON or default hardcoded values.
    /// </summary>
    public class Question
    {
        /// <summary>The question text displayed to the user</summary>
        public required string Text { get; set; }
        
        /// <summary>List of possible answers the user can select from</summary>
        public required List<Option> Options { get; set; }
    }

    /// <summary>
    /// Represents a single answer option for a question.
    /// Each option can affect multiple careers positively or negatively.
    /// </summary>
    public class Option
    {
        /// <summary>The text displayed for this answer choice</summary>
        public required string Text { get; set; }
        
        /// <summary>
        /// Dictionary mapping career names to point impacts.
        /// Positive values increase career scores, negative values decrease them.
        /// Example: { "Software Developer", 10 } adds 10 points to Software Developer
        /// </summary>
        public required Dictionary<string, int> Impact { get; set; }
        
        /// <summary>
        /// Optional badge name to award if this option is selected.
        /// Badges are displayed in the final report as achievements.
        /// </summary>
        public string? BadgeAwarded { get; set; }
    }

    // ==========================================
    // MAIN PROGRAM
    // ==========================================

    class Program
    {
        // ==========================================
        // GLOBAL STATE VARIABLES
        // ==========================================
        
        /// <summary>List of all available career paths being evaluated</summary>
        static List<Career> careers = new List<Career>();
        
        /// <summary>Dictionary for O(1) career lookups by name (optimized)</summary>
        static Dictionary<string, Career> careerLookup = new Dictionary<string, Career>();
        
        /// <summary>Collection of badges earned during the current assessment session</summary>
        static List<string> earnedBadges = new List<string>();
        
        /// <summary>Name of the current user taking the assessment</summary>
        static string userName = "Guest";
        
        /// <summary>File path for JSON-based question storage</summary>
        static string questionsFilePath = "questions.json";
        
        /// <summary>Cached JSON serializer options to avoid repeated allocation</summary>
        static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>
        /// Application entry point. Displays the main menu and handles navigation
        /// between different features: assessment, history loading, and admin mode.
        /// </summary>
        static void Main(string[] args)
        {
            // Enable UTF-8 encoding for special characters (badges, progress bars)
            Console.OutputEncoding = Encoding.UTF8;
            
            // Load career data into memory
            InitializeData();

            // Main application loop - continues until user exits
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
                
                // Read user input without displaying it (true parameter)
                var key = Console.ReadKey(true).Key;
                PlaySound(true); // Audible feedback for selection

                // Handle menu navigation - supports both number row and numpad
                switch (key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        RunAssessment(); // Start a new career assessment
                        break;
                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        LoadPreviousResult(); // View saved assessment results
                        break;
                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        RunAdminMode(); // Enter admin panel for debugging/testing
                        break;
                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        return; // Exit application
                }
            }
        }

        /// <summary>
        /// Runs the complete career assessment workflow.
        /// This includes: user introduction, question loop, scoring, tie-breaking,
        /// results display, and post-assessment actions (save/email).
        /// </summary>
        static void RunAssessment()
        {
            Console.Clear();
            DrawHeader();
            Console.WriteLine("\n  We will analyze your personality, skills, and work style.");
            
            // Capture user's name for personalization
            Console.Write("\n  Please enter your name: ");
            string? inputName = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(inputName)) userName = inputName;

            Console.WriteLine($"\n  Hello, {userName}. Press [ENTER] to begin assessment...");
            Console.ReadLine();

            // 1. Load Questions (JSON Integration)
            // Attempts to load from JSON file, falls back to hardcoded defaults
            var questions = LoadQuestionsWithFallback();
            
            // 2. The Questions Loop - Ask each question and track responses
            int currentQ = 1;
            ResetScores(); // Reset all career scores and badges for fresh assessment

            // Iterate through all assessment questions
            foreach (var q in questions)
            {
                // Display interactive menu and wait for user selection
                var selectedOption = ShowInteractiveMenu(q, currentQ, questions.Count);
                
                // Apply points to careers based on the selected answer
                ApplyScore(selectedOption);
                
                // Provide audible feedback
                PlaySound(true);
                
                // Visual feedback - simulate processing time
                ShowLoadingBar("Processing...", 15); 
                currentQ++;
            }

            // 3. Calculate Results & Tie-Breaker Logic
            // Sort careers by score in descending order (highest first) - ONCE
            var sortedCareers = careers.OrderByDescending(c => c.Score).ToList();
            
            // Check for close race: If top 2 careers are within 5 points, trigger tie-breaker
            // This adds excitement and ensures a decisive winner
            if (sortedCareers.Count >= 2 && sortedCareers[0].Score - sortedCareers[1].Score < 5)
            {
                RunTieBreaker(sortedCareers[0], sortedCareers[1]);
                // Re-sort careers after additional points from tie-breaker
                sortedCareers = careers.OrderByDescending(c => c.Score).ToList();
            }

            var topCareer = sortedCareers[0];

            // 4. Final Dashboard
            DisplayDashboard(sortedCareers, topCareer);

            // 5. Post-Assessment Options (Save / Email)
            HandlePostAssessment(sortedCareers, topCareer);
        }

        // ==========================================
        // FEATURE IMPLEMENTATIONS
        // ==========================================

        /// <summary>
        /// Resets all career scores to zero and clears earned badges.
        /// Called at the start of each new assessment to ensure clean slate.
        /// </summary>
        static void ResetScores()
        {
            // Reset each career's accumulated points to 0
            foreach(var c in careers) c.Score = 0;
            
            // Clear badge collection from any previous session
            earnedBadges.Clear();
        }

        /// <summary>
        /// Attempts to load questions from a JSON file. If file doesn't exist or fails to load,
        /// falls back to hardcoded default questions and saves them to JSON for future use.
        /// This provides extensibility - admins can modify questions.json to customize assessments.
        /// </summary>
        /// <returns>List of Question objects ready for the assessment</returns>
        static List<Question> LoadQuestionsWithFallback()
        {
            // JSON Integration: Try to load from file first (allows customization)
            if (File.Exists(questionsFilePath))
            {
                try
                {
                    // Read JSON file content
                    string jsonString = File.ReadAllText(questionsFilePath);
                    
                    // Deserialize JSON into Question objects using cached options
                    var loadedQuestions = JsonSerializer.Deserialize<List<Question>>(jsonString);
                    
                    // Validate that we got valid questions
                    if (loadedQuestions != null && loadedQuestions.Count > 0)
                        return loadedQuestions;
                }
                catch (Exception ex)
                {
                    // If JSON is corrupted or invalid, inform user and fallback
                    Console.WriteLine($"  [!] Error loading JSON: {ex.Message}. Using defaults.");
                }
            }

            // Fallback to hardcoded questions and save them for future editing
            var defaults = GetDefaultQuestions();
            try
            {
                // Use cached JSON options instead of creating new ones
                string jsonString = JsonSerializer.Serialize(defaults, jsonOptions);
                
                // Write to file so users can modify questions externally
                File.WriteAllText(questionsFilePath, jsonString);
            }
            catch { /* Ignore write errors in restricted environments (read-only filesystems) */ }

            return defaults;
        }

        /// <summary>
        /// Triggers a sudden-death tie-breaker question when two careers are too close in score.
        /// Creates a dramatic moment and ensures a decisive winner by forcing a final choice.
        /// The winning choice gets +10 points, the losing choice gets -5 points.
        /// </summary>
        /// <param name="c1">First career in the tie</param>
        /// <param name="c2">Second career in the tie</param>
        static void RunTieBreaker(Career c1, Career c2)
        {
            // Alert sound (different from success sound) to signal dramatic moment
            PlaySound(false);
            
            Console.Clear();
            DrawHeader();
            
            // Display warning in yellow to emphasize the tie-breaker situation
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  ⚠ TIE DETECTED! SUDDEN DEATH ROUND ⚠");
            Console.ResetColor();
            Console.WriteLine($"  It is too close to call between {c1.Name} and {c2.Name}.");
            
            // Dynamically create a tie-breaker question specific to these two careers
            var q = new Question
            {
                Text = $"If you had to choose one path right now, which appeals more?",
                Options = new List<Option>
                {
                    // Option 1: Boost first career, penalize second
                    new Option { Text = $"Focus on {c1.Name} tasks", Impact = new Dictionary<string, int> { { c1.Name, 10 }, { c2.Name, -5 } } },
                    
                    // Option 2: Boost second career, penalize first
                    new Option { Text = $"Focus on {c2.Name} tasks", Impact = new Dictionary<string, int> { { c2.Name, 10 }, { c1.Name, -5 } } }
                }
            };

            // Present the tie-breaker question and apply the score difference
            var decision = ShowInteractiveMenu(q, 99, 99); // Use 99 to indicate special question
            ApplyScore(decision);
        }

        /// <summary>
        /// Admin/God Mode - Development and testing feature that allows direct manipulation
        /// of career scores and viewing raw statistics. Protected by a simple password.
        /// Useful for testing different result scenarios without completing full assessments.
        /// </summary>
        static void RunAdminMode()
        {
            Console.Clear();
            DrawHeader();
            Console.Write("\n  Enter Admin Password: ");
            string? pass = Console.ReadLine();
            
            // Simple password protection (hardcoded for demo - use proper auth in production)
            if (pass != "1234")
            {
                PlaySound(false); // Error sound
                Console.WriteLine("  Access Denied.");
                Thread.Sleep(1000);
                return; // Exit admin mode if password is wrong
            }

            PlaySound(true); // Success sound for correct password
            
            // Admin mode loop - continues until user chooses to exit
            while (true)
            {
                Console.Clear();
                
                // Display "dangerous" admin header in red
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  *** ADMIN / GOD MODE ***");
                Console.ResetColor();
                
                Console.WriteLine("  1. View Raw Career Stats");
                Console.WriteLine("  2. Add Bonus Points to a Career");
                Console.WriteLine("  3. Return to Main Menu");
                
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.D3) return; // Exit admin mode

                // Option 1: Display current scores for all careers
                if (key == ConsoleKey.D1)
                {
                    foreach(var c in careers) Console.WriteLine($"  - {c.Name}: {c.Score} pts");
                    Console.ReadKey(); // Wait for acknowledgment
                }
                
                // Option 2: Manually boost a career's score (for testing)
                if (key == ConsoleKey.D2)
                {
                    Console.WriteLine("\n  Add 50 pts to Software Developer? (Y/N)");
                    if (Console.ReadKey(true).Key == ConsoleKey.Y)
                    {
                        // Use dictionary lookup instead of LINQ for better performance
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

        /// <summary>
        /// Loads and displays previously saved assessment results from text files.
        /// Allows users to review their past assessments and compare results over time.
        /// Searches for files matching the pattern "CareerPath_*.txt" in the current directory.
        /// </summary>
        static void LoadPreviousResult()
        {
            Console.Clear();
            DrawHeader();
            
            // Search current directory for saved assessment files
            string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "CareerPath_*.txt");
            
            // Handle case where no previous assessments exist
            if (files.Length == 0)
            {
                Console.WriteLine("\n  No history found.");
                Console.WriteLine("  Press any key to return.");
                Console.ReadKey();
                return;
            }

            // Display numbered list of available saved results
            Console.WriteLine("\n  Select a file to load:");
            for(int i=0; i<files.Length; i++)
            {
                // Show only filename, not full path, for cleaner display
                Console.WriteLine($"  {i+1}. {Path.GetFileName(files[i])}");
            }

            // Get user's file selection
            Console.Write("\n  Choice: ");
            if(int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= files.Length)
            {
                // Read and display the selected historical report
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

        /// <summary>
        /// Presents post-assessment action menu allowing user to save results to file,
        /// email results, or return to main menu.
        /// </summary>
        /// <param name="sorted">List of careers sorted by score (descending)</param>
        /// <param name="top">The highest-scoring career recommendation</param>
        static void HandlePostAssessment(List<Career> sorted, Career top)
        {
            Console.WriteLine("\n  ACTIONS:");
            Console.WriteLine("  [S] Save to File");
            Console.WriteLine("  [E] Email Results");
            Console.WriteLine("  [ENTER] Return to Menu");
            
            var key = Console.ReadKey(true).Key;
            
            // Route to appropriate handler based on user choice
            if (key == ConsoleKey.S)
            {
                SaveResults(sorted, top);
            }
            else if (key == ConsoleKey.E)
            {
                EmailResults(sorted, top);
            }
            // Any other key (including ENTER) returns to main menu
        }

        /// <summary>
        /// Simulates emailing assessment results to the user.
        /// In production, this would connect to an SMTP server with proper credentials.
        /// Currently runs in simulation mode to avoid runtime errors without valid SMTP setup.
        /// </summary>
        /// <param name="sorted">List of careers sorted by score</param>
        /// <param name="top">Top recommended career</param>
        static void EmailResults(List<Career> sorted, Career top)
        {
            // Email Integration Feature
            Console.Write("\n  Enter your email address: ");
            string? email = Console.ReadLine();

            // Basic email validation (check for @ symbol)
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                Console.WriteLine("  Invalid email.");
                return;
            }

            Console.WriteLine($"  Sending report to {email}...");
            ShowLoadingBar("Connecting to SMTP", 20);

            // NOTE: In a real app, you need valid SMTP credentials. 
            // This is a simulation for demonstration purposes to prevent crashing.
            // Set to false and configure SMTP settings below for actual email functionality.
            bool simulation = true; 

            if (simulation)
            {
                Console.WriteLine("  [SIMULATION] Email sent successfully!");
            }
            else
            {
                try
                {
                    // Example implementation (Commented out to prevent runtime errors without creds)
                    /*
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
                    */
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  Error sending email: " + ex.Message);
                }
            }
            Thread.Sleep(1000);
        }

        // ==========================================
        // HELPERS & VISUALS
        // ==========================================

        /// <summary>
        /// Plays audible feedback using system beep (Windows only).
        /// Provides audio cues to enhance user experience during navigation and assessment.
        /// </summary>
        /// <param name="success">True for positive action (high pitch), false for alert/error (low pitch)</param>
        static void PlaySound(bool success)
        {
            try
            {
                // Only attempt beep on Windows systems (other OS don't support Console.Beep)
                if (OperatingSystem.IsWindows())
                {
                    if (success) 
                        Console.Beep(1000, 100); // High pitch (1000 Hz), short duration (100ms)
                    else 
                        Console.Beep(200, 300); // Low pitch (200 Hz), longer duration (300ms)
                }
            }
            catch { /* Ignore on systems without speakers or when audio is disabled */ }
        }

        /// <summary>
        /// Initializes the career database with all available career paths.
        /// Called once at application startup to populate the careers list.
        /// Each career includes name, salary range, description, and starts with 0 score.
        /// </summary>
        static void InitializeData()
        {
            careers.Clear(); // Ensure clean slate
            careerLookup.Clear(); // Clear lookup dictionary
            
            // Add Software Developer career path
            var softwareDev = new Career 
            { 
                Name = "Software Developer", 
                SalaryRange = "$70k - $120k",
                Description = "You build the systems that run the world. You love logic, problem-solving, and seeing code come to life.",
                Score = 0 
            };
            careers.Add(softwareDev);
            careerLookup[softwareDev.Name] = softwareDev;
            
            var uiuxDesigner = new Career 
            { 
                Name = "UI/UX Designer", 
                SalaryRange = "$65k - $110k",
                Description = "You bridge the gap between human and machine. You care about aesthetics, user empathy, and intuitive flows.",
                Score = 0 
            };
            careers.Add(uiuxDesigner);
            careerLookup[uiuxDesigner.Name] = uiuxDesigner;
            
            var dataAnalyst = new Career 
            { 
                Name = "Data Analyst", 
                SalaryRange = "$60k - $100k",
                Description = "You turn noise into knowledge. You love patterns, statistics, and finding the truth hidden in spreadsheets.",
                Score = 0 
            };
            careers.Add(dataAnalyst);
            careerLookup[dataAnalyst.Name] = dataAnalyst;
            
            var cyberSecurity = new Career 
            { 
                Name = "Cybersecurity Analyst", 
                SalaryRange = "$75k - $130k",
                Description = "The digital guardian. You enjoy breaking things to fix them, analyzing threats, and protecting systems.",
                Score = 0 
            };
            careers.Add(cyberSecurity);
            careerLookup[cyberSecurity.Name] = cyberSecurity;
        }

        static List<Question> GetDefaultQuestions()
        {
            return new List<Question>
            {
                // Includes "Negative Scoring" examples (Negative Impact)
                new Question
                {
                    Text = "How do you feel about advanced mathematics?",
                    Options = new List<Option>
                    {
                        new Option { Text = "I love it.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 }, { "Software Developer", 5 } } },
                        new Option { Text = "It's okay if necessary.", Impact = new Dictionary<string, int> { { "Software Developer", 2 } } },
                        new Option { Text = "I hate it.", Impact = new Dictionary<string, int> { { "Data Analyst", -10 }, { "UI/UX Designer", 5 } } }, // Negative Score
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
                    Text = "You find a USB drive in the parking lot. You:",
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
                        new Option { Text = "A creative studio with mood boards.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 10 } } },
                        new Option { Text = "Quiet room, headphones on, multiple monitors.", Impact = new Dictionary<string, int> { { "Software Developer", 8 }, { "Data Analyst", 8 } } },
                        new Option { Text = "A command center watching live traffic.", Impact = new Dictionary<string, int> { { "Cybersecurity Analyst", 10 } } },
                        new Option { Text = "Collaborative open space.", Impact = new Dictionary<string, int> { { "UI/UX Designer", 5 }, { "Software Developer", 5 } } }
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
                        new Option { Text = "Collecting stock market data.", Impact = new Dictionary<string, int> { { "Data Analyst", 10 } } }
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

        /// <summary>
        /// Applies the scoring impact of a selected answer option to relevant careers.
        /// Also awards badges if the option grants one and it hasn't been earned yet.
        /// Supports both positive and negative scoring (e.g., +10 points or -5 points).
        /// </summary>
        /// <param name="option">The user's selected answer option containing impact data</param>
        static void ApplyScore(Option option)
        {
            // Apply points to affected careers using O(1) dictionary lookup
            // Each option can impact multiple careers with different point values
            foreach (var impact in option.Impact)
            {
                // Use dictionary for O(1) lookup instead of FirstOrDefault O(n) search
                if (careerLookup.TryGetValue(impact.Key, out var career))
                {
                    // Add points (can be positive or negative)
                    career.Score += impact.Value;
                }
            }

            // Award Badge (if this option grants one)
            // Badges are achievement markers displayed in final results
            if (!string.IsNullOrEmpty(option.BadgeAwarded))
            {
                // Prevent duplicate badges - only add if not already earned
                if (!earnedBadges.Contains(option.BadgeAwarded))
                    earnedBadges.Add(option.BadgeAwarded);
            }
        }

        // ==========================================
        // UI ENGINE (Simulating Spectre.Console)
        // ==========================================

        /// <summary>
        /// Displays an interactive arrow-key navigable menu for question answering.
        /// Simulates the Spectre.Console selection prompt with visual highlighting.
        /// User navigates with UP/DOWN arrows and confirms selection with ENTER.
        /// </summary>
        /// <param name="q">The question to display</param>
        /// <param name="current">Current question number (for progress display)</param>
        /// <param name="total">Total number of questions (for progress display)</param>
        /// <returns>The selected Option object</returns>
        static Option ShowInteractiveMenu(Question q, int current, int total)
        {
            int index = 0; // Currently highlighted option index
            ConsoleKey key;

            // Input loop - continues until user presses ENTER
            do
            {
                // Redraw menu each time to show new selection
                Console.Clear();
                DrawHeader();
                Console.WriteLine($"\n  QUESTION {current} of {total}: {q.Text}");
                Console.WriteLine("  (Use UP/DOWN Arrows to navigate, ENTER to select)\n");

                // Display all options with visual highlighting for current selection
                for (int i = 0; i < q.Options.Count; i++)
                {
                    if (i == index)
                    {
                        // Highlighted option: inverted colors (black text on cyan background)
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.BackgroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"  > {q.Options[i].Text}  ");
                        Console.ResetColor();
                    }
                    else
                    {
                        // Non-highlighted options: normal display
                        Console.WriteLine($"    {q.Options[i].Text}");
                    }
                }

                // Read next key press
                key = Console.ReadKey(true).Key;

                // Handle UP arrow - move selection up (with wraparound)
                if (key == ConsoleKey.UpArrow)
                {
                    index--;
                    if (index < 0) index = q.Options.Count - 1; // Wrap to bottom
                }
                // Handle DOWN arrow - move selection down (with wraparound)
                else if (key == ConsoleKey.DownArrow)
                {
                    index++;
                    if (index >= q.Options.Count) index = 0; // Wrap to top
                }

            } while (key != ConsoleKey.Enter); // Exit loop when ENTER is pressed

            // Return the option at the current index
            return q.Options[index];
        }

        /// <summary>
        /// Draws the application header/banner displayed at the top of most screens.
        /// Provides consistent branding and visual identity throughout the application.
        /// </summary>
        static void DrawHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine("       CAREER PATH DECISION SYSTEM v3.0           ");
            Console.WriteLine("==================================================");
            Console.ResetColor();
        }

        /// <summary>
        /// Displays an animated progress bar to simulate processing and provide visual feedback.
        /// Creates a more engaging user experience during transitions.
        /// </summary>
        /// <param name="message">Text to display before the progress bar</param>
        /// <param name="speed">Milliseconds delay between each bar segment (lower = faster)</param>
        static void ShowLoadingBar(string message, int speed = 30)
        {
            Console.Write($"\n  {message} [");
            
            // Draw 20 segments progressively
            for (int i = 0; i < 20; i++)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("█"); // Full block character
                Console.ResetColor();
                Thread.Sleep(speed); // Animate by delaying between segments
            }
            
            Console.WriteLine("] Done.");
            Thread.Sleep(200); // Brief pause before continuing
        }

        /// <summary>
        /// Displays the comprehensive assessment results dashboard.
        /// Shows top recommendation, score visualization, and earned badges.
        /// This is the main results screen users see after completing the assessment.
        /// </summary>
        /// <param name="sortedCareers">Careers sorted by score (highest first)</param>
        /// <param name="topCareer">The #1 recommended career</param>
        static void DisplayDashboard(List<Career> sortedCareers, Career topCareer)
        {
            Console.Clear();
            DrawHeader();
            Console.WriteLine($"\n  ASSESSMENT COMPLETE FOR: {userName.ToUpper()}");
            Console.WriteLine("  --------------------------------------------------");

            // Highlight top recommendation prominently in green
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ★ TOP RECOMMENDATION: {topCareer.Name.ToUpper()} ★");
            Console.ResetColor();
            Console.WriteLine($"  Salary Range: {topCareer.SalaryRange}");
            Console.WriteLine($"  {WrapText(topCareer.Description, 60)}");

            // Visual bar chart showing relative scores for all careers
            Console.WriteLine("\n  MATCH ANALYSIS");
            Console.WriteLine("  --------------------------------------------------");
            foreach (var career in sortedCareers)
            {
                DrawBarChart(career.Name, career.Score, 50); // Max width 50 chars
            }

            // Display achievements/badges earned during assessment
            if (earnedBadges.Count > 0)
            {
                Console.WriteLine("\n  EARNED BADGES");
                Console.WriteLine("  --------------------------------------------------");
                
                // Optimize badge display by building the line first
                var badgeDisplay = new System.Text.StringBuilder("  ");
                foreach (var badge in earnedBadges)
                {
                    badgeDisplay.Append($"[{badge}] ");
                }
                Console.WriteLine(badgeDisplay.ToString().TrimEnd());
            }
            
            Console.WriteLine("\n  Thank you for using the Career Path Decision System!"); // Inclusive closing line
            Console.WriteLine("  Press any key to return to the main menu.");
            Console.ReadKey();
        }

        /// <summary>
        /// Draws a horizontal bar chart representing the score of a career.
        /// Visually compares all careers at a glance, aiding in result comprehension.
        /// </summary>
        /// <param name="careerName">The name of the career to draw</param>
        /// <param name="score">The score value of the career</param>
        /// <param name="maxWidth">Maximum width of the bar in characters</param>
        static void DrawBarChart(string careerName, int score, int maxWidth)
        {
            // Scale score to fit within the maxWidth of the chart
            int scaledWidth = (int)((double)score / 130 * maxWidth);
            
            // Clamp to ensure within bounds
            if (scaledWidth < 0) scaledWidth = 0;
            if (scaledWidth > maxWidth) scaledWidth = maxWidth;
            
            // Draw the bar with a dynamic number of segments
            Console.Write($"    {careerName}: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(new string('█', scaledWidth) + new string('─', maxWidth - scaledWidth));
            Console.ResetColor();
        }

        /// <summary>
        /// Wraps text to a specified line width, breaking at spaces for readability.
        /// </summary>
        /// <param name="text">The input text to wrap</param>
        /// <param name="lineWidth">The maximum width of each line</param>
        /// <returns>Wrapped text with new line characters</returns>
        static string WrapText(string text, int lineWidth)
        {
            var sb = new System.Text.StringBuilder();
            int currentLineLength = 0;
            
            // Split by spaces to find word boundaries
            foreach (var word in text.Split(' '))
            {
                // If adding this word exceeds the limit, wrap to next line
                if (currentLineLength + word.Length + 1 > lineWidth)
                {
                    sb.AppendLine();
                    currentLineLength = 0;
                }
                else if (currentLineLength > 0)
                {
                    // Add a space before the next word, if not at beginning of the line
                    sb.Append(' ');
                    currentLineLength++;
                }
                
                // Add the word to the line
                sb.Append(word);
                currentLineLength += word.Length;
            }
            
            return sb.ToString();
        }
        
        static string GenerateReportString(List<Career> sorted, Career top)
        {
            // Pre-allocate StringBuilder with estimated capacity to reduce reallocations
            var sb = new StringBuilder(512);
            
            // Header with user info and timestamp
            sb.AppendLine("CAREER PATH REPORT");
            sb.Append("User: ").AppendLine(userName);
            sb.Append("Date: ").AppendLine(DateTime.Now.ToString());
            sb.AppendLine("--------------------------------");
            
            // Top recommendation summary
            sb.Append("Top Recommendation: ").AppendLine(top.Name);
            sb.Append("Potential Salary: ").AppendLine(top.SalaryRange);
            sb.AppendLine("--------------------------------");
            
            // Complete score breakdown for all careers
            sb.AppendLine("Full Breakdown:");
            foreach (var c in sorted)
            {
                sb.Append("- ").Append(c.Name).Append(": ").Append(c.Score).AppendLine(" points");
            }
            sb.AppendLine("--------------------------------");
            
            // Badges earned during assessment
            sb.AppendLine("Badges Earned:");
            foreach(var b in earnedBadges) 
            {
                sb.Append("* ").AppendLine(b);
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Saves assessment results to a text file in the current directory.
        /// Filename includes username and date for easy identification and historical tracking.
        /// </summary>
        /// <param name="sorted">Careers sorted by score</param>
        /// <param name="top">Top recommended career</param>
        static void SaveResults(List<Career> sorted, Career top)
        {
            // Generate unique filename with username and date (format: CareerPath_UserName_20260108.txt)
            string fileName = $"CareerPath_{userName}_{DateTime.Now:yyyyMMdd}.txt";
            
            // Generate formatted report content
            string content = GenerateReportString(sorted, top);

            // Write to file in current directory
            File.WriteAllText(fileName, content);
            
            // Confirm successful save to user
            Console.WriteLine($"\n  [SUCCESS] Report saved to {fileName}");
            Console.WriteLine("  Press any key to return.");
            Console.ReadKey();
        }
    }
}