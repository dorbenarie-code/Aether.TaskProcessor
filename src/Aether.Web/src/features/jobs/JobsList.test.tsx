import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { JobStatus, type JobDto } from "../../api/jobsApi";
import { JobsList } from "./JobsList";

vi.mock("../../api/jobsApi", async () => {
  const actual = await vi.importActual<typeof import("../../api/jobsApi")>(
    "../../api/jobsApi",
  );

  return {
    ...actual,
    getJobs: vi.fn(),
    getJobById: vi.fn(),
  };
});

import { getJobById, getJobs } from "../../api/jobsApi";

const getJobsMock = vi.mocked(getJobs);
const getJobByIdMock = vi.mocked(getJobById);

describe("JobsList", () => {
  beforeEach(() => {
    getJobsMock.mockReset();
    getJobByIdMock.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("renders jobs returned from the API client", async () => {
    const jobs: JobDto[] = [
      {
        id: "8f5c0c7a-1111-4444-8888-123456789abc",
        jobType: "PrintMessage",
        status: JobStatus.Completed,
        retryCount: 0,
        maxRetries: 3,
        errorMessage: null,
        createdAtUtc: "2026-01-01T10:00:00Z",
        startedAtUtc: "2026-01-01T10:00:01Z",
        completedAtUtc: "2026-01-01T10:00:02Z",
        nextRetryAtUtc: null,
      },
    ];

    getJobsMock.mockResolvedValue(jobs);

    render(<JobsList />);

    expect(screen.getByText("Loading jobs...")).toBeInTheDocument();

    expect(await screen.findByText("PrintMessage")).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Completed" })).toBeInTheDocument();
    expect(screen.getByText("0 / 3")).toBeInTheDocument();
    expect(getJobsMock).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 20,
    });
  });

  it("loads jobs by selected status", async () => {
    const failedJobs: JobDto[] = [
      {
        id: "7c3e0a2b-2222-4444-9999-123456789abc",
        jobType: "AlwaysFailingMessage",
        status: JobStatus.Failed,
        retryCount: 3,
        maxRetries: 3,
        errorMessage: "Simulated permanent failure.",
        createdAtUtc: "2026-01-01T10:00:00Z",
        startedAtUtc: "2026-01-01T10:00:01Z",
        completedAtUtc: "2026-01-01T10:00:02Z",
        nextRetryAtUtc: null,
      },
    ];

    getJobsMock.mockResolvedValueOnce([]);
    getJobsMock.mockResolvedValueOnce(failedJobs);

    render(<JobsList />);

    expect(await screen.findByText("No jobs found.")).toBeInTheDocument();

    fireEvent.change(screen.getByRole("combobox", { name: "Status" }), {
      target: { value: String(JobStatus.Failed) },
    });

    expect(await screen.findByText("AlwaysFailingMessage")).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Failed" })).toBeInTheDocument();
    expect(getJobsMock).toHaveBeenLastCalledWith({
      status: JobStatus.Failed,
      page: 1,
      pageSize: 20,
    });
  });

  it("loads the next page when clicking Next", async () => {
    const firstPageJobs: JobDto[] = [
      {
        id: "11111111-1111-4444-8888-123456789abc",
        jobType: "FirstPageJob",
        status: JobStatus.Completed,
        retryCount: 0,
        maxRetries: 3,
        errorMessage: null,
        createdAtUtc: "2026-01-01T10:00:00Z",
        startedAtUtc: "2026-01-01T10:00:01Z",
        completedAtUtc: "2026-01-01T10:00:02Z",
        nextRetryAtUtc: null,
      },
    ];

    const secondPageJobs: JobDto[] = [
      {
        id: "22222222-2222-4444-8888-123456789abc",
        jobType: "SecondPageJob",
        status: JobStatus.Processing,
        retryCount: 1,
        maxRetries: 3,
        errorMessage: null,
        createdAtUtc: "2026-01-01T10:05:00Z",
        startedAtUtc: "2026-01-01T10:05:01Z",
        completedAtUtc: null,
        nextRetryAtUtc: null,
      },
    ];

    getJobsMock.mockResolvedValueOnce(firstPageJobs);
    getJobsMock.mockResolvedValueOnce(secondPageJobs);

    render(<JobsList />);

    expect(await screen.findByText("FirstPageJob")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Next" }));

    expect(await screen.findByText("SecondPageJob")).toBeInTheDocument();
    expect(screen.getByText("Page 2")).toBeInTheDocument();
    expect(getJobsMock).toHaveBeenLastCalledWith({
      page: 2,
      pageSize: 20,
    });
  });

  it("renders details for a selected job", async () => {
    const job: JobDto = {
      id: "8f5c0c7a-1111-4444-8888-123456789abc",
      jobType: "PrintMessage",
      status: JobStatus.Completed,
      retryCount: 0,
      maxRetries: 3,
      errorMessage: null,
      createdAtUtc: "2026-01-01T10:00:00Z",
      startedAtUtc: "2026-01-01T10:00:01Z",
      completedAtUtc: "2026-01-01T10:00:02Z",
      nextRetryAtUtc: null,
    };

    getJobsMock.mockResolvedValueOnce([job]);
    getJobByIdMock.mockResolvedValueOnce(job);

    render(<JobsList />);

    expect(await screen.findByText("PrintMessage")).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole("button", { name: "View details for PrintMessage" }),
    );

    expect(await screen.findByText("Job Details")).toBeInTheDocument();
    expect(screen.getByText(job.id)).toBeInTheDocument();
    expect(screen.getByText("Created at")).toBeInTheDocument();
    expect(screen.getByText("2026-01-01T10:00:00Z")).toBeInTheDocument();
    expect(getJobByIdMock).toHaveBeenCalledWith(job.id);
  });

  it("renders an error message when loading jobs fails", async () => {
    getJobsMock.mockRejectedValue(new Error("API unavailable"));

    render(<JobsList />);

    expect(await screen.findByText("Failed to load jobs.")).toBeInTheDocument();
  });
});
