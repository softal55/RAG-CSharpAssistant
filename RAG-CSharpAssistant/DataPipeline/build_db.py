import json
import sqlite3
import os
import re
import hashlib
from bs4 import BeautifulSoup
import textwrap

# Configuration
INPUT_FILE = "stack_overflow_c#_data.jsonl"
DB_NAME = "csharp_knowledge.db"
CHUNK_SIZE = 800
MAX_CHUNKS = 150000  # Keeps DB mobile-friendly (~100–150MB)


def clean_html_preserve_code(html_content):
    if not html_content:
        return ""

    soup = BeautifulSoup(html_content, "lxml")

    for code_block in soup.find_all(["code", "pre"]):
        code_text = code_block.get_text()
        code_block.replace_with(f"\n[CODE]\n{code_text}\n[/CODE]\n")

    text = soup.get_text()

    # Preserve structure
    return "\n".join(line.strip() for line in text.splitlines() if line.strip())


def safe_truncate_code(code, max_size):
    if len(code) <= max_size:
        return code

    truncated = code[:max_size]

    # Try to cut at meaningful boundary
    for sep in ["\n", ";", "}"]:
        idx = truncated.rfind(sep)
        if idx > max_size * 0.6:
            return truncated[: idx + 1]

    return truncated


def smart_chunk_text(text, max_size):
    parts = re.split(r"(\[CODE\].*?\[/CODE\])", text, flags=re.DOTALL)
    chunks = []

    for part in parts:
        if not part.strip():
            continue

        if part.startswith("[CODE]"):
            safe_code = safe_truncate_code(part.strip(), max_size)
            chunks.append(safe_code)
        elif len(part) <= max_size:
            chunks.append(part.strip())
        else:
            wrapped = textwrap.wrap(
                part, max_size, break_long_words=False, replace_whitespace=False
            )
            chunks.extend(wrapped)

    return chunks


def build_database():
    print(f"Initializing database: {DB_NAME}...")

    if os.path.exists(DB_NAME):
        os.remove(DB_NAME)

    conn = sqlite3.connect(DB_NAME)
    cursor = conn.cursor()

    # Performance tweaks
    cursor.execute("PRAGMA journal_mode=WAL;")
    cursor.execute("PRAGMA synchronous=NORMAL;")

    # FTS5 with C#-friendly tokenizer
    cursor.execute("""
        CREATE VIRTUAL TABLE qa_index USING fts5(
            chunk,
            source_question,
            tags,
            tokenize = "unicode61 remove_diacritics 2 tokenchars '#<>._'"
        );
    """)

    cursor.execute("""
        CREATE TABLE metadata (
            rowid INTEGER PRIMARY KEY,
            score REAL,
            is_accepted INTEGER
        );
    """)

    # Index for fast JOIN
    cursor.execute("CREATE INDEX idx_metadata_rowid ON metadata(rowid);")

    print("Processing JSONL file...")

    inserted_chunks = 0
    total_chars = 0
    seen_chunks = set()

    with open(INPUT_FILE, "r", encoding="utf-8") as f:
        for line in f:
            if inserted_chunks >= MAX_CHUNKS:
                print(f"Reached MAX_CHUNKS limit ({MAX_CHUNKS})")
                break

            if not line.strip():
                continue

            try:
                data = json.loads(line)

                title = data.get("title", "")
                question_html = data.get("question", "")
                answer_html = data.get("answer", "")

                raw_tags = data.get("tags", [])
                tags = " ".join(raw_tags + raw_tags[:1])  # balanced weighting

                score = float(data.get("question_score", 0))
                is_accepted = 1 if data.get("is_accepted", True) else 0

                clean_q = clean_html_preserve_code(question_html)
                clean_a = clean_html_preserve_code(answer_html)

                # Skip weak answers
                if len(clean_a) < 30:
                    continue

                answer_label = "[ACCEPTED SOLUTION]" if is_accepted else "[SOLUTION]"

                full_text = f"""[TITLE]
{title}

[TAGS]
{tags}

[QUESTION]
{clean_q}

{answer_label}
{clean_a}"""
                question_only = f"""[TITLE]
{title}

[TAGS]
{tags}

[QUESTION]
{clean_q}"""

                chunks = smart_chunk_text(full_text, CHUNK_SIZE)
                chunks += smart_chunk_text(question_only, CHUNK_SIZE // 2)

                for chunk in chunks:
                    chunk_hash = hashlib.md5(chunk.encode("utf-8")).hexdigest()

                    if chunk_hash in seen_chunks:
                        continue
                    seen_chunks.add(chunk_hash)

                    cursor.execute(
                        """
                        INSERT INTO qa_index (chunk, source_question, tags)
                        VALUES (?, ?, ?)
                    """,
                        (chunk, title, tags),
                    )

                    last_id = cursor.lastrowid

                    cursor.execute(
                        """
                        INSERT INTO metadata (rowid, score, is_accepted)
                        VALUES (?, ?, ?)
                    """,
                        (last_id, score, is_accepted),
                    )

                    inserted_chunks += 1
                    total_chars += len(chunk)

            except json.JSONDecodeError:
                continue

    conn.commit()
    conn.close()

    avg_chunk_size = total_chars / inserted_chunks if inserted_chunks else 0
    db_size = os.path.getsize(DB_NAME) / (1024 * 1024)

    print("\nPipeline Complete!")
    print(f"Total Chunks: {inserted_chunks}")
    print(
        f"Average Chunk Size: {avg_chunk_size:.0f} chars (~{avg_chunk_size / 4:.0f} tokens)"
    )
    print(f"Database Size: {db_size:.1f} MB")
    print(f"Saved to: {DB_NAME}")


if __name__ == "__main__":
    build_database()
