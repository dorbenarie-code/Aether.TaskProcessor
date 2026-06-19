import { useState } from "react";
import { submitJob, type SubmitJobResponseDto } from "../../api/jobsApi";

type SubmitState =
  | { status: "idle" }
  | { status: "submitting" }
  | { status: "submitted"; job: SubmitJobResponseDto }
  | { status: "failed" };

export function SubmitJobForm() {
  const [jobType, setJobType] = useState("");
  const [payload, setPayload] = useState("");
  const [maxRetries, setMaxRetries] = useState(3);
  const [submitState, setSubmitState] = useState<SubmitState>({
    status: "idle",
  });

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setSubmitState({ status: "submitting" });

    try {
      const job = await submitJob({
        jobType,
        payload,
        maxRetries,
      });

      setSubmitState({ status: "submitted", job });
    } catch {
      setSubmitState({ status: "failed" });
    }
  }

  return (
    <section>
      <h2>Submit Job</h2>

      <form onSubmit={handleSubmit}>
        <div>
          <label>
            Job Type
            <input
              value={jobType}
              onChange={(event) => setJobType(event.target.value)}
              required
            />
          </label>
        </div>

        <div>
          <label>
            Payload
            <textarea
              value={payload}
              onChange={(event) => setPayload(event.target.value)}
              required
            />
          </label>
        </div>

        <div>
          <label>
            Max Retries
            <input
              type="number"
              min="0"
              value={maxRetries}
              onChange={(event) => setMaxRetries(Number(event.target.value))}
            />
          </label>
        </div>

        <button type="submit" disabled={submitState.status === "submitting"}>
          {submitState.status === "submitting" ? "Submitting..." : "Submit Job"}
        </button>
      </form>

      {renderSubmitState(submitState)}
    </section>
  );
}

function renderSubmitState(state: SubmitState) {
  if (state.status === "idle" || state.status === "submitting") {
    return null;
  }

  if (state.status === "failed") {
    return <p>Failed to submit job.</p>;
  }

  return (
    <div>
      <p>Job submitted successfully.</p>
      <p>{state.job.id}</p>
    </div>
  );
}
