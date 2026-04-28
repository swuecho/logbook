import argparse
import os
import random
import statistics
import string
import time
from datetime import datetime, timezone

import requests


def iso_utc_now() -> str:
    # Emit a stable UTC timestamp string similar to .NET's default "O" / ISO-8601 UTC ("...Z").
    # Some parsers are picky about "+00:00" vs "Z".
    return datetime.utcnow().replace(microsecond=0).isoformat() + 'Z'


def rand_suffix(n: int = 7) -> str:
    alphabet = string.ascii_lowercase + string.digits
    return ''.join(random.choice(alphabet) for _ in range(n))


def rand_note_id_8() -> str:
    # DB schema: diary.note_id is varchar(8)
    return rand_suffix(8)


def tiptap_todo_doc_json_string(text: str) -> str:
    # Must be a JSON *string* in the Diary.Note field (matches IntegrationTests.fs)
    doc = {
        'type': 'doc',
        'content': [
            {
                'type': 'taskList',
                'content': [
                    {
                        'type': 'taskItem',
                        'attrs': {'checked': False},
                        'content': [
                            {
                                'type': 'paragraph',
                                'content': [{'type': 'text', 'text': text}],
                            }
                        ],
                    }
                ],
            }
        ],
    }
    import json

    return json.dumps(doc, ensure_ascii=False)


def tiptap_paragraph_doc_json_string(text: str) -> str:
    doc = {
        'type': 'doc',
        'content': [
            {
                'type': 'paragraph',
                'content': [{'type': 'text', 'text': text}],
            }
        ],
    }
    import json

    return json.dumps(doc, ensure_ascii=False)


def percentile(sorted_values: list[float], p: float) -> float:
    if not sorted_values:
        return float('nan')
    if p <= 0:
        return sorted_values[0]
    if p >= 100:
        return sorted_values[-1]
    k = (len(sorted_values) - 1) * (p / 100.0)
    f = int(k)
    c = min(f + 1, len(sorted_values) - 1)
    if f == c:
        return sorted_values[f]
    d0 = sorted_values[f] * (c - k)
    d1 = sorted_values[c] * (k - f)
    return d0 + d1


def main() -> None:
    parser = argparse.ArgumentParser(description='Insert diary notes (optionally with todos).')
    parser.add_argument('--base-url', default=os.getenv('LOGBOOK_BASE_URL', 'http://localhost:5000'))
    parser.add_argument('--username', default=os.getenv('LOGBOOK_USERNAME', f'loadtest-{rand_suffix()}@example.test'))
    parser.add_argument('--password', default=os.getenv('LOGBOOK_PASSWORD', 'password'))
    parser.add_argument('--count', type=int, default=100)
    parser.add_argument('--timeout-seconds', type=float, default=15.0)
    parser.add_argument(
        '--pace-ms',
        type=float,
        default=0.0,
        help='If set, sleep this many milliseconds between inserts to avoid flooding background queues.',
    )
    parser.add_argument('--check-todo-endpoint', action='store_true')
    parser.add_argument(
        '--wait-todo-seconds',
        type=float,
        default=0.0,
        help='If set, poll /api/todo up to this many seconds to observe background todo extraction.',
    )
    parser.add_argument(
        '--mode',
        choices=['todo', 'plain'],
        default='todo',
        help='todo: TipTap taskList/taskItem (hits todo extraction); plain: paragraph only',
    )
    args = parser.parse_args()

    session = requests.Session()
    session.headers.update({'Content-Type': 'application/json'})

    login_payload = {'username': args.username, 'password': args.password}
    login_url = f'{args.base_url}/api/login'
    r = session.post(login_url, json=login_payload, timeout=args.timeout_seconds)
    r.raise_for_status()
    access_token = r.json()['accessToken']
    session.headers.update({'Authorization': f'Bearer {access_token}'})

    latencies_ms: list[float] = []
    note_ids: list[str] = []

    for i in range(args.count):
        note_id = rand_note_id_8()
        note_ids.append(note_id)

        todo_text = f'#{i + 1} inserted by insert_100_notes_with_todos'
        note_json = (
            tiptap_todo_doc_json_string(todo_text)
            if args.mode == 'todo'
            else tiptap_paragraph_doc_json_string(todo_text)
        )
        diary_payload = {
            'id': 0,
            'userId': 0,
            'noteId': note_id,
            'note': note_json,
            'lastUpdated': iso_utc_now(),
        }

        url = f'{args.base_url}/api/diary/{note_id}'
        t0 = time.perf_counter()
        resp = session.put(url, json=diary_payload, timeout=args.timeout_seconds)
        dt_ms = (time.perf_counter() - t0) * 1000.0
        latencies_ms.append(dt_ms)

        if resp.status_code != 200:
            raise RuntimeError(f'Failed on {note_id}: {resp.status_code} {resp.text}')

        if args.pace_ms and args.pace_ms > 0:
            time.sleep(args.pace_ms / 1000.0)

    lat_sorted = sorted(latencies_ms)
    p50 = percentile(lat_sorted, 50)
    p95 = percentile(lat_sorted, 95)
    p99 = percentile(lat_sorted, 99)
    mean = statistics.mean(latencies_ms) if latencies_ms else float('nan')
    mx = max(latencies_ms) if latencies_ms else float('nan')

    print(f'Inserted {args.count} notes.')
    print(f'Latency ms: mean={mean:.1f} p50={p50:.1f} p95={p95:.1f} p99={p99:.1f} max={mx:.1f}')

    if args.check_todo_endpoint:
        todo_url = f'{args.base_url}/api/todo'
        deadline = time.time() + max(0.0, args.wait_todo_seconds)
        last_missing: list[str] = note_ids
        sleep_s = 0.25

        while True:
            todo_resp = session.get(todo_url, timeout=args.timeout_seconds)
            todo_resp.raise_for_status()
            body = todo_resp.text
            missing = [nid for nid in note_ids if nid not in body]
            last_missing = missing

            if not missing:
                print('Verified: /api/todo includes inserted noteIds.')
                break

            if time.time() >= deadline:
                raise RuntimeError(
                    f'/api/todo response missing {len(missing)} inserted noteIds (showing 3): {missing[:3]}'
                )

            time.sleep(sleep_s)
            sleep_s = min(2.0, sleep_s * 1.5)


if __name__ == '__main__':
    main()

