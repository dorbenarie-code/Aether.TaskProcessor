import { useEffect, useState } from "react";
import {
  getJobById,
  getJobs,
  JobStatus,
  type JobDto,
} from "../../api/jobsApi";

type JobsState =
  | { status: "loading" }
  | { status: "loaded"; jobs: JobDto[] }
  | { status: "failed" };

type JobDetailsState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "loaded"; job: JobDto }
  | { status: "failed" };

const allStatusValue = "all";
const pageSize = 20;

export function JobsList() {
  const [selectedStatus, setSelectedStatus] = useState(allStatusValue);
  const [page, setPage] = useState(1);
  const [state, setState] = useState<JobsState>({ status: "loading" });
  const [detailsState, setDetailsState] = useState<JobDetailsState>({
    status: "idle",
  });

  useEffect(() => {
    let isMounted = true;

    setState({ status: "loading" });

    const status =
      selectedStatus === allStatusValue
        ? undefined
        : (Number(selectedStatus) as JobStatus);

    getJobs({
      status,
      page,
      pageSize,
    })
      .then((jobs) => {
        if (isMounted) {
          setState({ status: "loaded", jobs });
        }
      })
      .catch(() => {
        if (isMounted) {
          setState({ status: "failed" });
        }
      });

    return () => {
      isMounted = false;
    };
  }, [selectedStatus, page]);

  function handleStatusChange(value: string) {
    setSelectedStatus(value);
    setPage(1);
    setDetailsState({ status: "idle" });
  }

  async function handleViewDetails(jobId: string) {
    setDetailsState({ status: "loading" });

    try {
      const job = await getJobById(jobId);
      setDetailsState({ status: "loaded", job });
    } catch {
      setDetailsState({ status: "failed" });
    }
  }

  return (
    <section>
      <h2>Jobs</h2>

      <label>
        Status
        <select
          value={selectedStatus}
          onChange={(event) => handleStatusChange(event.target.value)}
        >
          <option value={allStatusValue}>All</option>
          <option value={JobStatus.Pending}>Pending</option>
          <option value={JobStatus.Processing}>Processing</option>
          <option value={JobStatus.Completed}>Completed</option>
          <option value={JobStatus.Failed}>Failed</option>
          <option value={JobStatus.Cancelled}>Cancelled</option>
        </select>
      </label>

      {renderJobsState(state, handleViewDetails)}

      <nav aria-label="Jobs pagination">
        <button
          type="button"
          disabled={page === 1}
          onClick={() => setPage((currentPage) => Math.max(1, currentPage - 1))}
        >
          Previous
        </button>

        <span>Page {page}</span>

        <button
          type="button"
          onClick={() => {
            setPage((currentPage) => currentPage + 1);
            setDetailsState({ status: "idle" });
          }}
        >
          Next
        </button>
      </nav>

      {renderJobDetailsState(detailsState)}
    </section>
  );
}

function renderJobsState(
  state: JobsState,
  onViewDetails: (jobId: string) => void,
) {
  if (state.status === "loading") {
    return <p>Loading jobs...</p>;
  }

  if (state.status === "failed") {
    return <p>Failed to load jobs.</p>;
  }

  if (state.jobs.length === 0) {
    return <p>No jobs found.</p>;
  }

  return (
    <table>
      <thead>
        <tr>
          <th>Job Type</th>
          <th>Status</th>
          <th>Retries</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        {state.jobs.map((job) => (
          <tr key={job.id}>
            <td>{job.jobType}</td>
            <td>{formatJobStatus(job.status)}</td>
            <td>
              {job.retryCount} / {job.maxRetries}
            </td>
            <td>
              <button
                type="button"
                onClick={() => onViewDetails(job.id)}
                aria-label={`View details for ${job.jobType}`}
              >
                Details
              </button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function renderJobDetailsState(state: JobDetailsState) {
  if (state.status === "idle") {
    return null;
  }

  if (state.status === "loading") {
    return <p>Loading job details...</p>;
  }

  if (state.status === "failed") {
    return <p>Failed to load job details.</p>;
  }

  const { job } = state;

  return (
    <aside aria-label="Job details">
      <h3>Job Details</h3>

      <dl>
        <dt>Id</dt>
        <dd>{job.id}</dd>

        <dt>Job Type</dt>
        <dd>{job.jobType}</dd>

        <dt>Status</dt>
        <dd>{formatJobStatus(job.status)}</dd>

        <dt>Retries</dt>
        <dd>
          {job.retryCount} / {job.maxRetries}
        </dd>

        <dt>Error</dt>
        <dd>{job.errorMessage ?? "None"}</dd>

        <dt>Created at</dt>
        <dd>{job.createdAtUtc}</dd>

        <dt>Started at</dt>
        <dd>{job.startedAtUtc ?? "Not started"}</dd>

        <dt>Completed at</dt>
        <dd>{job.completedAtUtc ?? "Not completed"}</dd>

        <dt>Next retry at</dt>
        <dd>{job.nextRetryAtUtc ?? "None"}</dd>
      </dl>
    </aside>
  );
}

function formatJobStatus(status: JobStatus): string {
  switch (status) {
    case JobStatus.Pending:
      return "Pending";
    case JobStatus.Processing:
      return "Processing";
    case JobStatus.Completed:
      return "Completed";
    case JobStatus.Failed:
      return "Failed";
    case JobStatus.Cancelled:
      return "Cancelled";
  }
}
