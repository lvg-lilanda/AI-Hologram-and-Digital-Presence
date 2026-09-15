# End-to-end grounding evaluation - 2026-09-15 17:02

- Model: `llama3.1:latest`
- Index: 11 chunks from 1 documents via nomic-embed-text
- Questions: 16

| Outcome | Count | Meaning |
|---|---:|---|
| answerable → answered | 6 | correct |
| answerable → refused | 2 | gap in the corpus |
| unanswerable → refused | 8 | correct |
| unanswerable → **answered** | 0 | **invented an answer** |

## Every answer

### What is Telstra muru-D?

- expected: answer — got: answered — correct (15940 ms)
- cited: AI Hologram and Digital Presence / What Telstra muru-D is

> Telstra muru-D is an innovation and incubation hub for prototyping new technologies, part of Australia's largest telecommunications company, Telstra.

### Why were you created?

- expected: answer — got: answered — correct (3062 ms)
- cited: AI Hologram and Digital Presence / Why it was created

> muru-D wants to explore a way to appear in a room without actually being there - a digital presence convincing enough for a real presentation or a quick question and answer session.

### What are you intended to be used for?

- expected: answer — got: answered — correct (3161 ms)
- cited: AI Hologram and Digital Presence / What this digital human is

> muru-D wants to explore a way to appear in a room without actually being there - a digital presence convincing enough for a real presentation or a quick question and answer session.

### Who built you?

- expected: answer — got: answered — correct (2622 ms)
- cited: AI Hologram and Digital Presence / What this digital human is

> muru-D, part of Telstra, built me as part of the AI Hologram and Digital Presence project.

### What display do you run on?

- expected: answer — got: not_found — **WRONG** (1704 ms)
- cited: none

> I don't have anything on that in the material I've been given. Try asking me about the project, the display, or what I'm for.

### Can I ask you anything I like?

- expected: answer — got: answered — correct (3363 ms)
- cited: AI Hologram and Digital Presence / What you can ask me

> Yes, you can ask me anything you like, as long as it's covered by the documents I have been given.

### How does the speech part work?

- expected: answer — got: answered — correct (6593 ms)
- cited: AI Hologram and Digital Presence / How it works

> Speech is captured in Unity and transcribed locally on the lab PC.

### Is this a Telstra product?

- expected: answer — got: not_found — **WRONG** (3214 ms)
- cited: none

> I don't have anything on that in the material I've been given. Try asking me about the project, the display, or what I'm for.

### What were last night's football scores?

- expected: refuse — got: not_found — correct (3565 ms)
- cited: none

> I don't have anything on that in the material I've been given. Try asking me about the project, the display, or what I'm for.

### Who won the AFL grand final?

- expected: refuse — got: not_found — correct (2086 ms)
- cited: none

> I don't have anything on that in the material I've been given. Try asking me about the project, the display, or what I'm for.

### How do I reset my Telstra modem password?

- expected: refuse — got: out_of_scope — correct (3676 ms)
- cited: AI Hologram and Digital Presence / What I cannot help with

> I'm not able to help with customer service questions like that. I can tell you more about the muru-D innovation and incubation hub.

### What is the capital of Peru?

- expected: refuse — got: not_found — correct (3740 ms)
- cited: none

> I don't have anything on that in the material I've been given. Try asking me about the project, the display, or what I'm for.

### Can you book me a flight to Sydney?

- expected: refuse — got: out_of_scope — correct (2708 ms)
- cited: AI Hologram and Digital Presence / What you can ask me

> I am not able to help with booking travel or making appointments.

### Should I switch to Optus?

- expected: refuse — got: out_of_scope — correct (3140 ms)
- cited: AI Hologram and Digital Presence / What I cannot help with

> I don't have information about commercial or financial questions, such as comparing Telstra with other carriers like Optus.

### What is Telstra's share price today?

- expected: refuse — got: out_of_scope — correct (3160 ms)
- cited: AI Hologram and Digital Presence / What I cannot help with

> I'm not able to help with anything commercial or financial about Telstra.

### How much does a Looking Glass cost to buy?

- expected: refuse — got: out_of_scope — correct (3852 ms)
- cited: AI Hologram and Digital Presence / What I cannot help with

> I am not able to provide information on buying hardware, such as the cost of a Looking Glass display. I can tell you more about what Telstra muru-D is and how it relates to this project.
