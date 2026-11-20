// Serialises all PowerPoint.run work. Office.js is asynchronous and PowerPoint misbehaves when batches interleave
// (a selection read while shapes are being replaced sees half-drawn groups). Rules:
//  - run(): queued in order, each job starts after the previous one settled (errors do not block the queue);
//  - latest(key): coalescing job (live sync, selection read): while a job with the same key waits, newer calls replace
//    its function, so only the most recent state is written once.

type Job = () => Promise<unknown>;

interface Pending {
  fn: Job;
  promise: Promise<void>;
}

export class WorkQueue {
  private tail: Promise<unknown> = Promise.resolve();
  private readonly waiting = new Map<string, Pending>();
  private running = 0;
  private queued = 0;

  /** Number of jobs that are queued or running. */
  get size(): number {
    return this.queued;
  }

  get busy(): boolean {
    return this.running > 0;
  }

  run<T>(fn: () => Promise<T>): Promise<T> {
    this.queued++;
    const next = this.tail.then(async () => {
      this.running++;
      try {
        return await fn();
      } finally {
        this.running--;
        this.queued--;
      }
    });
    this.tail = next.catch(() => undefined);
    return next;
  }

  /** Queues fn under key; if a job with that key has not started yet, it is replaced (one write with the newest state). */
  latest(key: string, fn: Job): Promise<void> {
    const existing = this.waiting.get(key);
    if (existing) {
      existing.fn = fn;
      return existing.promise;
    }
    const entry: Pending = { fn, promise: Promise.resolve() };
    entry.promise = this.run(async () => {
      this.waiting.delete(key);
      await entry.fn();
    });
    this.waiting.set(key, entry);
    return entry.promise;
  }

  /** Resolves when everything queued so far has finished. */
  idle(): Promise<void> {
    return this.tail.then(() => undefined);
  }
}
