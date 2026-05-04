# DataPipeline

Builds the offline knowledge base used by the .NET MAUI app.

## Files

| File | Purpose |
| ---- | ------- |
| `stack_overflow_c#_data.jsonl` | Raw Stack Overflow C# Q&A export (input). |
| `build_db.py`                  | Pipeline that cleans HTML, smart-chunks the text, and writes the FTS5 SQLite DB. |
| `requirements.txt`             | Python dependencies (`beautifulsoup4`, `lxml`). |
| `csharp_knowledge.db`          | Output. **Generated** — bundled into the MAUI app as a `MauiAsset`. |

## Running the pipeline

```powershell
cd RAG-CSharpAssistant\DataPipeline
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
python build_db.py
```

This produces `csharp_knowledge.db` next to the script (~100–150 MB). The MAUI
project references it conditionally:

```xml
<MauiAsset Include="DataPipeline\csharp_knowledge.db"
           Condition="Exists('DataPipeline\csharp_knowledge.db')"
           LogicalName="csharp_knowledge.db" />
```

So just rebuild the MAUI app after the DB is generated and it will be embedded.

## Schema

The pipeline writes two tables:

```sql
CREATE VIRTUAL TABLE qa_index USING fts5(
    chunk,
    source_question,
    tags,
    tokenize = "unicode61 remove_diacritics 2 tokenchars '#<>._'"
);

CREATE TABLE metadata (
    rowid       INTEGER PRIMARY KEY,
    score       REAL,
    is_accepted INTEGER
);
```

`Services/RagSearchService.cs` queries them with a BM25-ranked join, biased
toward accepted answers and high-scored questions:

```sql
SELECT qa_index.rowid, qa_index.chunk, qa_index.source_question, qa_index.tags,
       m.score, m.is_accepted, bm25(qa_index) AS rank
FROM qa_index
JOIN metadata m ON m.rowid = qa_index.rowid
WHERE qa_index MATCH $q
ORDER BY rank ASC, m.is_accepted DESC, m.score DESC
LIMIT $topK;
```

## Runtime flow

1. App launch → `RagSearchService.EnsureInitializedAsync` copies the bundled
   `csharp_knowledge.db` from the app package to `FileSystem.AppDataDirectory`
   (one-time, since SQLite needs a writable file path on most platforms).
2. User submits a question → `MainPageViewModel` calls
   `RagSearchService.SearchAsync` and streams the formatted top-K matches into
   the active bot bubble token-by-token.
3. If the DB is missing (pipeline never ran), the assistant replies with a
   clear "run `build_db.py` and rebuild" message instead of silently failing.
