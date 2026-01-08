using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using CareerPathRecommender;

namespace CareerPathRecommender.Benchmarks
{
    [MemoryDiagnoser]
    public class CareerSystemBenchmarks
    {
        private List<Career> _careers = null !;
        private List<Question> _questions = null !;
        private List<Career> _sortedCareers = null !;
        [GlobalSetup]
        public void Setup()
        {
            // Initialize careers
            _careers = new List<Career>
            {
                new Career
                {
                    Name = "Software Developer",
                    SalaryRange = "$70k - $120k",
                    Description = "You build the systems that run the world. You love logic, problem-solving, and seeing code come to life.",
                    Score = 0
                },
                new Career
                {
                    Name = "UI/UX Designer",
                    SalaryRange = "$65k - $110k",
                    Description = "You bridge the gap between human and machine. You care about aesthetics, user empathy, and intuitive flows.",
                    Score = 0
                },
                new Career
                {
                    Name = "Data Analyst",
                    SalaryRange = "$60k - $100k",
                    Description = "You turn noise into knowledge. You love patterns, statistics, and finding the truth hidden in spreadsheets.",
                    Score = 0
                },
                new Career
                {
                    Name = "Cybersecurity Analyst",
                    SalaryRange = "$75k - $130k",
                    Description = "The digital guardian. You enjoy breaking things to fix them, analyzing threats, and protecting systems.",
                    Score = 0
                }
            };
            // Create sample questions
            _questions = new List<Question>
            {
                new Question
                {
                    Text = "How do you feel about advanced mathematics?",
                    Options = new List<Option>
                    {
                        new Option
                        {
                            Text = "I love it.",
                            Impact = new Dictionary<string, int>
                            {
                                {
                                    "Data Analyst",
                                    10
                                },
                                {
                                    "Software Developer",
                                    5
                                }
                            }
                        },
                        new Option
                        {
                            Text = "It's okay if necessary.",
                            Impact = new Dictionary<string, int>
                            {
                                {
                                    "Software Developer",
                                    2
                                }
                            }
                        },
                        new Option
                        {
                            Text = "I hate it.",
                            Impact = new Dictionary<string, int>
                            {
                                {
                                    "Data Analyst",
                                    -10
                                },
                                {
                                    "UI/UX Designer",
                                    5
                                }
                            }
                        }
                    }
                }
            };
            // Create sorted careers for report generation
            _sortedCareers = _careers.OrderByDescending(c => c.Score).ToList();
        }

        [Benchmark]
        public void ApplyScoreToMultipleCareers()
        {
            var option = new Option
            {
                Text = "Test option",
                Impact = new Dictionary<string, int>
                {
                    {
                        "Software Developer",
                        10
                    },
                    {
                        "Data Analyst",
                        5
                    },
                    {
                        "UI/UX Designer",
                        -3
                    }
                },
                BadgeAwarded = "Test Badge"
            };
            foreach (var impact in option.Impact)
            {
                var career = _careers.FirstOrDefault(c => c.Name == impact.Key);
                if (career != null)
                {
                    career.Score += impact.Value;
                }
            }
        }

        [Benchmark]
        public List<Career> SortCareersByScore()
        {
            return _careers.OrderByDescending(c => c.Score).ToList();
        }

        [Benchmark]
        public string GenerateReport()
        {
            var sb = new System.Text.StringBuilder();
            var top = _sortedCareers.First();
            sb.AppendLine("CAREER PATH REPORT");
            sb.AppendLine($"User: TestUser");
            sb.AppendLine($"Date: {DateTime.Now}");
            sb.AppendLine("--------------------------------");
            sb.AppendLine($"Top Recommendation: {top.Name}");
            sb.AppendLine($"Potential Salary: {top.SalaryRange}");
            sb.AppendLine("--------------------------------");
            sb.AppendLine("Full Breakdown:");
            foreach (var c in _sortedCareers)
            {
                sb.AppendLine($"- {c.Name}: {c.Score} points");
            }

            return sb.ToString();
        }

        [Benchmark]
        public void JsonSerializationOfQuestions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string jsonString = JsonSerializer.Serialize(_questions, options);
        }
    }
}