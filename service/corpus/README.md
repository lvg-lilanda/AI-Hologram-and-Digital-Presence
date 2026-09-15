# Approved corpus

Everything in this folder is what the avatar is allowed to say. Nothing else.

Rules:

1. A document only lands here after the BA has approved it. The avatar cannot
   distinguish a draft from a signed-off fact, so the gate is at the folder, not
   in the prompt.
2. Supported formats: `.md`, `.txt`, `.pdf`. Markdown is preferred - headings become
   the `section` in every citation, which is what makes an answer checkable.
3. Re-index after any change, or the service keeps serving the old text:

       cd service && python -m app.cli index

4. This README is skipped by the indexer.

Owner: Anuji (corpus content, T08) - Ali (indexing and retrieval behaviour, T15).
