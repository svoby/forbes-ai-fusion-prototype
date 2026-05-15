// Cursor SDK — local runtime agent triggered by GitHub Actions on issues.opened.
// Runs on a self-hosted Windows runner that has Unity installed.
// Each issue gets its own git worktree so parallel jobs don't conflict.
import { Agent, CursorAgentError } from "@cursor/sdk";

const {
  CURSOR_API_KEY,
  ISSUE_NUMBER,
  ISSUE_TITLE,
  ISSUE_BODY,
  ISSUE_BRANCH,
  WORKTREE_PATH,
  CURSOR_MODEL,
} = process.env;

if (!CURSOR_API_KEY) {
  console.error("CURSOR_API_KEY secret is not set. Add it in Settings → Secrets.");
  process.exit(1);
}

if (!WORKTREE_PATH) {
  console.error("WORKTREE_PATH is not set — the 'Create worktree' step must run first.");
  process.exit(1);
}

const prompt = `
You are an autonomous coding agent working on a Unity + Photon Fusion prototype.

## Your task

Implement GitHub issue #${ISSUE_NUMBER}.

**Title:** ${ISSUE_TITLE}

**Body:**
${ISSUE_BODY ?? "(no body)"}

## Environment

- You are working inside a git worktree at: ${WORKTREE_PATH}
- Your branch is already created and checked out: ${ISSUE_BRANCH}
- Unity is installed on this machine. You CAN run EditMode tests via:
    powershell -ExecutionPolicy Bypass -File tools\\run-editmode-tests.ps1
  Important: close the Unity Editor for this project before running tests in batchmode.
- You have access to the gh CLI for opening pull requests.

## Mandatory reading before touching any file

1. Read \`AGENTS.md\` — source of truth for all workflow, branching, commit, and PR rules.
2. Read \`docs/AGENT_CONTEXT.md\` — current task context bundles.
3. Read \`docs/architecture.md\` — architecture and script responsibilities.
4. Read any files listed in the issue's "Required context" section.

## Required workflow (from AGENTS.md — do not skip steps)

1. **Fetch and verify** — confirm the issue is not stale, incomplete, contradictory, or out of scope. Stop and report if it is.
2. **Read context first** — read every file in the issue's required context bundle before editing.
3. **Implement only what the issue authorizes** — no scope creep, no opportunistic refactors.
4. **Test loop** — after each substantive change, run EditMode tests:
       powershell -ExecutionPolicy Bypass -File tools\\run-editmode-tests.ps1
   Fix failures and re-run until green. Do not open a PR with red tests.
5. **Verify the diff** — run \`git diff master\` and confirm only authorized files are changed.
6. **Commit** — write the commit message to \`.git/COMMIT_EDITMSG\`, then commit.
7. **Push** — push the branch: \`git push -u origin ${ISSUE_BRANCH}\`
8. **Open a PR** — use \`gh pr create\` with:
   - Title: exactly as the issue specifies (or a clear summary if not specified)
   - Body must include: Summary, Changed files, Context read, Verification performed (paste test output), Known risks / follow-ups, \`Closes #${ISSUE_NUMBER}\`
   - Base: master

## Architecture constraints (non-negotiable)

- Gameplay state changes are authoritative on **Host / State Authority only**.
- Run core gameplay simulation in Fusion ticks (\`FixedUpdateNetwork\`), not \`Update\`/\`LateUpdate\`.
- One responsibility per MonoBehaviour; extract pure logic into testable C# helpers.
- Do not change serialized defaults, prefab tuning, scenes, or \`.meta\` files unless the issue requires it.
- No \`FindObjectsByType\` or full-scene scans on hot simulation paths.

## Definition of Done

- EditMode tests green (paste results in PR body).
- No authority violations.
- Final diff is intentional and scoped to the issue.
- PR is open with a complete body.
`.trim();

const model = CURSOR_MODEL ? { id: CURSOR_MODEL } : undefined;
console.log(`Issue:      #${ISSUE_NUMBER} — ${ISSUE_TITLE}`);
console.log(`Branch:     ${ISSUE_BRANCH}`);
console.log(`Worktree:   ${WORKTREE_PATH}`);
console.log(`Model:      ${model?.id ?? "account default"}`);

const agent = Agent.create({
  apiKey: CURSOR_API_KEY,
  ...(model && { model }),
  local: { cwd: WORKTREE_PATH },
});

try {
  const run = await agent.send(prompt);
  console.log(`Agent started. run.id=${run.id}  agent.agentId=${agent.agentId}`);

  if (run.supports("stream")) {
    for await (const event of run.stream()) {
      if (event.type === "assistant") {
        for (const block of event.message.content) {
          if (block.type === "text") process.stdout.write(block.text);
        }
      }
    }
  }

  const result = await run.wait();

  if (result.status === "finished") {
    console.log(`\nAgent finished successfully. run.id=${result.id}`);
    process.exit(0);
  } else {
    console.error(`\nAgent run ended with status="${result.status}". run.id=${result.id}`);
    process.exit(2);
  }
} catch (err) {
  if (err instanceof CursorAgentError) {
    console.error(`Agent failed to start: ${err.message} (retryable=${err.isRetryable})`);
    process.exit(1);
  }
  throw err;
} finally {
  await agent[Symbol.asyncDispose]();
}
