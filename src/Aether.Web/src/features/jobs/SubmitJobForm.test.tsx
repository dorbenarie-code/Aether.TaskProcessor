import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { JobStatus, type SubmitJobResponseDto } from "../../api/jobsApi";
import { SubmitJobForm } from "./SubmitJobForm";

vi.mock("../../api/jobsApi", async () => {
  const actual = await vi.importActual<typeof import("../../api/jobsApi")>(
    "../../api/jobsApi",
  );

  return {
    ...actual,
    submitJob: vi.fn(),
  };
});

import { submitJob } from "../../api/jobsApi";

const submitJobMock = vi.mocked(submitJob);

describe("SubmitJobForm", () => {
  beforeEach(() => {
    submitJobMock.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("submits a job using the API client", async () => {
    const submittedJob: SubmitJobResponseDto = {
      id: "8f5c0c7a-1111-4444-8888-123456789abc",
      jobType: "PrintMessage",
      status: JobStatus.Pending,
      retryCount: 0,
      maxRetries: 3,
      createdAtUtc: "2026-01-01T10:00:00Z",
    };

    submitJobMock.mockResolvedValueOnce(submittedJob);

    render(<SubmitJobForm />);

    fireEvent.change(screen.getByLabelText("Job Type"), {
      target: { value: "PrintMessage" },
    });

    fireEvent.change(screen.getByLabelText("Payload"), {
      target: { value: '{"message":"hello from form"}' },
    });

    fireEvent.change(screen.getByLabelText("Max Retries"), {
      target: { value: "3" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Submit Job" }));

    expect(
      await screen.findByText("Job submitted successfully."),
    ).toBeInTheDocument();

    expect(screen.getByText(submittedJob.id)).toBeInTheDocument();

    expect(submitJobMock).toHaveBeenCalledWith({
      jobType: "PrintMessage",
      payload: '{"message":"hello from form"}',
      maxRetries: 3,
    });
  });

  it("renders an error message when submitting fails", async () => {
    submitJobMock.mockRejectedValueOnce(new Error("Bad request"));

    render(<SubmitJobForm />);

    fireEvent.change(screen.getByLabelText("Job Type"), {
      target: { value: "PrintMessage" },
    });

    fireEvent.change(screen.getByLabelText("Payload"), {
      target: { value: '{"message":"hello from form"}' },
    });

    fireEvent.click(screen.getByRole("button", { name: "Submit Job" }));

    expect(
      await screen.findByText("Failed to submit job."),
    ).toBeInTheDocument();
  });
});
