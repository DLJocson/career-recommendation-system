# 🎯 Career Recommendation System

---

> A comprehensive console-based career assessment application that helps users discover their ideal career path through an interactive questionnaire. The system analyzes personality traits, skills, and work preferences to recommend careers in tech fields including Software Development, UI/UX Design, Data Analysis, and Cybersecurity.

---

## ✨ Features

- 🎮 **Interactive Assessment**: 21 carefully crafted questions with arrow-key navigation
- 🧠 **Smart Scoring System**: Supports both positive and negative scoring impacts
- ⚔️ **Tie-Breaker Mode**: Automatic sudden-death round when careers are closely matched
- 🏆 **Badge System**: Earn achievement badges based on your answers
- 📊 **Results Dashboard**: Visual bar chart display of career matches
- 📝 **JSON Integration**: Customizable questions via external JSON file
- 📜 **Historical Tracking**: Save and review past assessment results
- 📧 **Email Simulation**: Mock email functionality for sending results
- 🔧 **Admin Mode**: Developer tools for testing and score manipulation

---

## 🚀 How to Run

### 📋 Prerequisites
- **.NET 10 SDK** or later

### ▶️ Running the Application

1. **📥 Clone the repository**
   ```bash
   git clone https://github.com/DLJocson/career-recommendation-system
   cd career-recommendation-system
   ```

2. **🔨 Build and run**
   ```bash
   dotnet run --project career-recommendation-system
   ```

   Or simply:
   ```bash
   cd career-recommendation-system
   dotnet run
   ```

3. **🎯 Follow the on-screen prompts** to navigate the menu and complete your assessment

---

## 🎮 Controls

| Key | Action |
|-----|--------|
| **1-4** | Select menu options |
| **↑/↓** | Navigate question options |
| **Enter** | Confirm selection |
| **S** | Save results to file |
| **E** | Email results (simulation mode) |

---

## 💼 Career Paths Available

| Career | Salary Range | Description |
|--------|--------------|-------------|
| 💻 **Software Developer** | $70k - $120k | Build systems that run the world |
| 🎨 **UI/UX Designer** | $65k - $110k | Bridge the gap between human and machine |
| 📊 **Data Analyst** | $60k - $100k | Turn noise into knowledge |
| 🛡️ **Cybersecurity Analyst** | $75k - $130k | The digital guardian |

---

## 🎓 Sample Badges You Can Earn

- 🧩 **Puzzle Master** - For those who love logic puzzles
- 🎨 **Creative Soul** - For the artistic minds
- 📁 **Organizer** - For the detail-oriented
- 🕵️ **Investigator** - For the curious analysts
- ⚙️ **Engineer** - For the builders
- 🛡️ **Guardian** - For the protectors
- 🧠 **All-Knowing** - For the knowledge seekers
- 🔒 **Security Conscious** - For the cautious thinkers
- 📈 **Min-Maxer** - For the optimization enthusiasts

---

## 📂 Project Structure

```
career-recommendation-system/
├── career-recommendation-system/
│   ├── Program.cs                 # Main application code
│   └── career-recommendation-system.csproj  # Project configuration
├── .gitignore                     # Git ignore rules
├── .gitattributes                 # Git attributes
├── career-recommendation-system.slnx  # Solution file
└── README.md                      # This file

Generated at runtime:
├── questions.json                 # Customizable question bank (auto-generated on first run)
└── CareerPath_*.txt              # Saved assessment results
```

---

## 🛠️ Technical Details

- **Language**: C# 14.0
- **Framework**: .NET 10
- **Architecture**: Console Application
- **Data Format**: JSON for question persistence
- **Platform Support**: Cross-platform (Windows, macOS, Linux)
  - *Note: Audio beep feature is Windows-only*

---

## 🎨 User Experience Highlights

- ✅ **Clean Console Interface** with color-coded output
- ✅ **Animated Loading Bars** for engaging feedback
- ✅ **Arrow-Key Navigation** for intuitive selection
- ✅ **Progress Tracking** throughout the assessment
- ✅ **Visual Bar Charts** for result comparison
- ✅ **Achievement System** with collectible badges

---

## 📝 Customization

Want to customize the questions? Simply edit the `questions.json` file after the first run! The application automatically generates this file with the default questions, making it easy to tailor the assessment to your needs.

**Example customization:**
```json
{
  "Text": "Your custom question?",
  "Options": [
    {
      "Text": "Answer option 1",
      "Impact": {
        "Software Developer": 10,
        "Data Analyst": 5
      },
      "BadgeAwarded": "Custom Badge"
    }
  ]
}
```

---

## 🔐 Admin Mode

Access the admin panel with password `1234` to:
- 👁️ View raw career statistics
- ➕ Add bonus points to careers
- 🧪 Test different scoring scenarios

> **Note**: This is a development feature for testing purposes.

---

## 🧹 Clean Build

To remove build artifacts and generated files:
```bash
dotnet clean
```

To rebuild from scratch:
```bash
dotnet clean
dotnet build
```

---

## 👨‍💻 Author

**Dan Louie M. Jocson**

---

## 📄 License

This project is available for educational and personal use.

---

<div align="center">

### 🌟 Career Path Decision System v3.0 🌟

*Helping you discover your ideal career path through technology*

</div>