# Multi-turn evaluation - 2026-09-15 17:03

- Model: `llama3.1:latest`
- Index: 11 chunks from 1 documents via nomic-embed-text
- Turns: 9/11 behaved

## the one that caught the hallucination

*Verbatim from the first hands-on session, 14 Sept 2026. The last turn answered that muru-D 'focuses on supporting startups and entrepreneurs' - not in the corpus, straight from the model's training. Regression case.*

**1. What is Telstra muru-D?**

- outcome: answered (expected answered)
- cited: AI Hologram and Digital Presence / What Telstra muru-D is

> Telstra muru-D is an innovation and incubation hub for prototyping new technologies, part of Australia's largest telecommunications company, Telstra.

**2. who are you?**

- outcome: answered (expected answered)
- cited: AI Hologram and Digital Presence / What this digital human is

> I'm a digital human created as part of the AI Hologram and Digital Presence project with Telstra muru-D.

**3. what do you know about the current geo-politics?**

- outcome: not_found (expected not_found)
- cited: none

> I don't have anything on that in the material I've been given. Try asking me about the project, the display, or what I'm for.

**4. ok tell me about the project**

- outcome: answered (expected answered)
- cited: AI Hologram and Digital Presence / What it is intended to be used for

> The AI Hologram and Digital Presence project is an experimental showcase piece with Telstra muru-D, prototyping new technologies. It's still in development, and two potential use cases are being considered.

## pronoun follow-ups

*Retrieval sees almost no signal in turns 2 and 3 unless it is given the conversation.*

**1. What display do you run on?**

- outcome: answered (expected answered)
- cited: AI Hologram and Digital Presence / How it works

> The Looking Glass holographic display.

**2. why that one?**

- outcome: answered (expected answered)
- cited: AI Hologram and Digital Presence / How it works

> The passages don't specifically mention the reason for choosing the Looking Glass, but it's mentioned in passage 2 as part of how the technology works.

**3. and who chose it?**

- outcome: not_found (expected answered)  — **WRONG**
- cited: none

> I don't have anything on that in the material I've been given. Try asking me about the project, the display, or what I'm for.

- problem: expected answered, got not_found

## refusal does not poison the next turn

*A refused question sits in history. The turn after it must still work.*

**1. What is the capital of Peru?**

- outcome: out_of_scope (expected not_found)  — **WRONG**
- cited: AI Hologram and Digital Presence / What this digital human is

> I'm a digital human created to assist with questions about this project, but I don't have general knowledge or information on geography.

- problem: expected not_found, got out_of_scope

**2. What is Telstra muru-D?**

- outcome: answered (expected answered)
- cited: AI Hologram and Digital Presence / What Telstra muru-D is

> Telstra muru-D is an innovation and incubation hub for prototyping new technologies.

## topic change after an out-of-scope reply

**1. How do I reset my Telstra modem password?**

- outcome: out_of_scope (expected out_of_scope)
- cited: AI Hologram and Digital Presence / What I cannot help with

> I'm not able to help with Telstra customer service questions, such as resetting your modem password. However, I can discuss the capabilities of AI holograms and digital presence.

**2. Why were you created?**

- outcome: answered (expected answered)
- cited: AI Hologram and Digital Presence / What this digital human is

> I was created as part of the AI Hologram and Digital Presence project with Telstra muru-D to explore what it could mean for someone to have a digital presence in a room without physically being there.
