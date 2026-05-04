🚀 RAG-CSharpAssistant
<p align="center"> <img src="./Resources/Images/dotnet_bot.png" width="120" /> </p> <h3 align="center"> High-Performance C# AI Assistant powered by Hybrid RAG + Real-Time LLM Streaming </h3> <p align="center"> <b>Designed for reliability, speed, and real developer workflows</b> </p> <p align="center"> <img src="https://img.shields.io/badge/.NET-MAUI-blue?style=for-the-badge&logo=dotnet" /> <img src="https://img.shields.io/badge/Architecture-RAG-purple?style=for-the-badge" /> <img src="https://img.shields.io/badge/Database-SQLite-green?style=for-the-badge" /> <img src="https://img.shields.io/badge/LLM-Groq-orange?style=for-the-badge" /> <img src="https://img.shields.io/badge/Streaming-RealTime-red?style=for-the-badge" /> </p>
🧠 What This Project Does

RAG-CSharpAssistant is a cross-platform application built with .NET MAUI that answers C#/.NET questions using a hybrid AI system:

🔍 Local retrieval (SQLite FTS5) → finds relevant knowledge from a curated dataset
🤖 LLM generation via Groq → produces clear, structured answers
⚡ Streaming output → responses appear in real time

Instead of relying purely on AI, the system first searches for existing knowledge, then uses the model to generate a context-aware answer.

🏗️ System Architecture
⚙️ Tech Stack
Layer	Technology
UI	.NET MAUI (XAML + MVVM)
Backend	C# (.NET 10)
Database	SQLite (FTS5 full-text search)
AI	LLaMA 3.1 via Groq
Data Pipeline	Python (BeautifulSoup, chunking)
Streaming	Server-Sent Events (SSE)
🔥 Core Capabilities
🔍 Retrieval-Augmented Generation (RAG)
Local knowledge base (Stack Overflow–style data)
FTS5 search with ranking (BM25)
Returns top relevant code-aware chunks
⚡ Real-Time Streaming
Token-by-token generation
Immediate UI feedback
Smooth conversational experience
🎯 Query Validation Layer
Classifies if a question is C#/.NET related
Prevents irrelevant or misleading responses
🧩 Dual Answer Strategy
Scenario	Behavior
Context found	Answer grounded in retrieved data
No context	General model-based answer
Out of scope	Explicit rejection
📸 Screenshots

📍 Add your screenshots in /screenshots/

/screenshots/
  chat.png
  streaming.png
  empty.png
💬 Chat Interface

⚡ Streaming Response

🧪 Data Pipeline
pip install -r requirements.txt
python build_db.py

Pipeline steps:

Parse JSONL dataset
Clean HTML & extract code
Smart chunking (~800 chars)
Store in SQLite FTS5
🔄 End-to-End Flow
User → Validation → Retrieval → Prompt → LLM → Streaming UI
🧠 Engineering Highlights
Full RAG system implementation
Efficient SQLite FTS5 querying
Real-time LLM streaming integration
Clean MVVM architecture
Cross-platform deployment
🚀 Installation
git clone https://github.com/your-username/RAG-CSharpAssistant.git
cd RAG-CSharpAssistant
Add API Key
export GROQ_API_KEY=your_key

Or:

Resources/Raw/groq.key
🔮 Roadmap
📚 Expand knowledge base
🧠 Fine-tuned models
🐞 Debugging assistant mode
🌍 Multi-language support
👨‍💻 Author

Sofiane Taleb
AI Student @ University of Oran 1

GitHub: https://github.com/softal55
LinkedIn: https://linkedin.com/in/sofiane-taleb-61a466210
