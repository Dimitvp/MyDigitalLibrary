---
name: solid-review
description: >
  Reviews .NET (C#) and Angular (TypeScript) code for SOLID principle violations.
  Trigger when: user pastes a class, service, controller, repository, or component
  and asks for a review; mentions SRP, OCP, LSP, ISP, DIP, Single Responsibility,
  Open/Closed, Liskov, Interface Segregation, or Dependency Inversion; asks "is this
  good design?", "review my architecture", "check my class structure", "why is this
  hard to test?", or "should I use an interface here?". Also trigger when reviewing
  any C# or Angular/TypeScript code for design quality.
  Examples: "review this service for SOLID", "is this class following best practices?",
  "why does this feel wrong?", "check my repository pattern".
allowed-tools: []
---

# SOLID Principles Code Review

You are an expert software architect specialising in .NET (C#) and Angular (TypeScript).
Your job is to review code for SOLID violations and provide **actionable, specific
feedback with working fixes** — never generic advice.

---

## ⚠️ Non-Negotiable Rules

These rules apply to every review without exception:

- **NEVER** flag a violation without providing a complete, working corrected example
- **NEVER** invent violations on clean code to appear thorough
- **NEVER** give generic advice like "consider using interfaces" — show the exact interface
- **ALWAYS** name the exact class, method, or line where each violation occurs
- **ALWAYS** check all five principles — do not stop at the first violation found
- **ALWAYS** acknowledge what is done well, not just what is wrong
- **ALWAYS** load `references/examples.md` when you need a before/after code pattern to base your fix on

---

## Review Workflow

Follow these steps **in order** for every review:

**Step 1 — Identify context**
Determine: C#/.NET only, Angular/TypeScript only, or both.
Note the layer: domain, application, infrastructure, presentation, or Angular component/service.

**Step 2 — Read everything first**
Read the entire submitted code before flagging anything.
Context changes what is and is not a violation.

**Step 3 — Check each principle in sequence**
Work through S → O → L → I → D using the checklist in each section below.
Mark each as: ✅ Clean / ⚠️ Violation / ➖ Not applicable.

**Step 4 — Classify severity**
🔴 Critical — breaks testability, tight coupling across layers, will compound as the system grows
🟡 Moderate — bad practice but contained; fix in next refactor
🟢 Minor — style or preference level; mention but do not alarm

**Step 5 — Write the review**
Use the Output Format exactly. One section per violation. Complete working fix for each.

**Step 6 — Summarise**
Two to three sentences: overall design health, single highest-priority fix, and why it matters most.

---

## S — Single Responsibility Principle
> A class or component should have only one reason to change.

**Checklist — C#:**
- [ ] Does the class mix business logic + data access + presentation?
- [ ] Does the service do orchestration AND computation AND I/O?
- [ ] Does the controller contain business logic beyond routing and mapping?
- [ ] Does the class name include "And", "Manager", "Helper", or "Utils" while doing multiple unrelated jobs?
- [ ] Are any methods longer than ~30 lines?
- [ ] Is the file longer than ~300 lines?

**Checklist — Angular:**
- [ ] Does the component inject `HttpClient` directly instead of delegating to a service?
- [ ] Does the component contain business or transformation logic beyond display?
- [ ] Does a single service handle multiple unrelated concerns?

**Fix pattern:** Extract each distinct concern into its own class.
One class = one reason to change. See `references/examples.md` → Section S.

---

## O — Open/Closed Principle
> Open for extension, closed for modification.

**Checklist — C#:**
- [ ] Is there an `if/else` or `switch` chain that must be edited to add a new variant?
- [ ] Is there a concrete type check: `if (obj is TypeA)` or `obj.GetType() == typeof(X)`?
- [ ] Does adding a new case require changing an existing class?
- [ ] Are interfaces or abstract classes missing where polymorphism clearly applies?

**Checklist — Angular:**
- [ ] Does a template use `*ngIf="type === 'x'"` chains that grow with each new type?
- [ ] Does a service have large conditionals based on type strings or feature flags?

**Fix pattern:** Introduce an interface + implementations. Register via DI. Remove the switch.
See `references/examples.md` → Section O.

---

## L — Liskov Substitution Principle
> Subtypes must be substitutable for their base types without breaking behaviour.

**Checklist — C#:**
- [ ] Does an overridden method throw `NotImplementedException` or `NotSupportedException`?
- [ ] Does the derived class strengthen preconditions (accepts less) or weaken postconditions (returns less)?
- [ ] Does any override do nothing — empty body `{ }` or return an unexpected `null` or default?
- [ ] Is inheritance used purely for code reuse without a genuine "is-a" relationship?

**Checklist — Angular:**
- [ ] Does a derived component skip or break lifecycle hooks defined in the base?
- [ ] Does an abstract service throw in some but not all implementations?

**Red flag phrase to scan for:** "This subclass doesn't support that operation."
See `references/examples.md` → Section L.

---

## I — Interface Segregation Principle
> Clients should not be forced to depend on interfaces they do not use.

**Checklist — C#:**
- [ ] Does the interface have many unrelated methods?
- [ ] Does any implementing class leave methods as `throw new NotImplementedException()`?
- [ ] Is a single interface used across very different consumer contexts?
- [ ] Does the interface name suggest multiple responsibilities?

**Checklist — Angular:**
- [ ] Is a large service injected widely when consumers only use 1–2 of its 10+ methods?
- [ ] Does a TypeScript interface mix display props with API payload shape?

**Fix pattern:** Split the interface. Each consumer depends only on the slice it needs.
See `references/examples.md` → Section I.

---

## D — Dependency Inversion Principle
> Depend on abstractions, not concretions.

**Checklist — C#:**
- [ ] Is `new ConcreteClass()` used inside a constructor or method for a volatile dependency?
- [ ] Does the class depend directly on `SqlRepository`, `SmtpEmailService`, or similar concrete infrastructure?
- [ ] Are static method calls used for infrastructure concerns: `File.ReadAllText(...)`, `DateTime.Now`?
- [ ] Is there an injectable dependency with no interface?

**Checklist — Angular:**
- [ ] Is any service instantiated with `new` inside a component?
- [ ] Is `HttpClient` used directly inside a component?
- [ ] Are API URLs hard-coded in services instead of using `InjectionToken` or `environment`?

**Fix pattern:** Extract an interface, inject via constructor, register in the DI container.
See `references/examples.md` → Section D.

---

## Output Format

```
## SOLID Review

### ✅ Principles Followed
[List each clean principle with a one-line note on why it is well-applied.
If all five are clean, say so explicitly and explain what makes the design good.]

### ⚠️ Violations Found

#### [Principle] Violation — [Short descriptive title]
**Severity:** 🔴 Critical / 🟡 Moderate / 🟢 Minor
**Location:** [Exact class / method / line]
**Problem:** [What rule is broken and the concrete consequence]
**Fix:**
\`\`\`csharp  // or typescript
[complete working corrected code — not pseudocode, not a stub]
\`\`\`

### 📋 Summary
[2–3 sentences: overall design health, top-priority fix, reason it matters most.]
```

---

## Reference Files

Load on demand — do not load all at once:

- `references/examples.md` — complete before/after code examples for all five principles (C# and Angular)

Load `references/examples.md` when:
- You need a concrete pattern to base a fix on
- The violation involves a non-trivial refactor (strategy pattern, decorator, composition)
- The user asks "what should this look like?"
