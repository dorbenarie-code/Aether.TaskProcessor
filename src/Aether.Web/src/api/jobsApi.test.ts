import { afterEach, describe, expect, it, vi } from "vitest";
import {
  getJobById,
  getJobs,
  JobStatus,
  submitJob,
  type JobDto,
  type SubmitJobResponseDto,
} from "./jobsApi";

describe("getJobs", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("fetches jobs from the jobs API", async () => {
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

    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(jobs), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      }),
    );

    vi.stubGlobal("fetch", fetchMock);

    const result = await getJobs({
      baseUrl: "http://localhost:5008",
    });

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5008/api/jobs");
    expect(result).toEqual(jobs);
  });

  it("adds optional query parameters", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify([]), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      }),
    );

    vi.stubGlobal("fetch", fetchMock);

    await getJobs({
      baseUrl: "http://localhost:5008",
      status: JobStatus.Failed,
      page: 2,
      pageSize: 10,
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5008/api/jobs?status=4&page=2&pageSize=10",
    );
  });

  it("throws when the API request fails", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(null, {
        status: 500,
        statusText: "Internal Server Error",
      }),
    );

    vi.stubGlobal("fetch", fetchMock);

    await expect(getJobs()).rejects.toThrow(
      "Failed to fetch jobs. Status: 500",
    );
  });
});

describe("getJobById", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("fetches a job by id from the jobs API", async () => {
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

    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(job), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      }),
    );

    vi.stubGlobal("fetch", fetchMock);

    const result = await getJobById(job.id, {
      baseUrl: "http://localhost:5008",
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5008/api/jobs/8f5c0c7a-1111-4444-8888-123456789abc",
    );
    expect(result).toEqual(job);
  });

  it("throws when the job does not exist", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(null, {
        status: 404,
        statusText: "Not Found",
      }),
    );

    vi.stubGlobal("fetch", fetchMock);

    await expect(getJobById("missing-job-id")).rejects.toThrow(
      "Job 'missing-job-id' was not found.",
    );
  });
});

describe("submitJob", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("submits a job to the jobs API", async () => {
    const responseBody: SubmitJobResponseDto = {
      id: "8f5c0c7a-1111-4444-8888-123456789abc",
      jobType: "PrintMessage",
      status: JobStatus.Pending,
      retryCount: 0,
      maxRetries: 3,
      createdAtUtc: "2026-01-01T10:00:00Z",
    };

    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(responseBody), {
        status: 201,
        headers: {
          "Content-Type": "application/json",
        },
      }),
    );

    vi.stubGlobal("fetch", fetchMock);

    const result = await submitJob(
      {
        jobType: "PrintMessage",
        payload: '{"message":"hello from frontend"}',
        maxRetries: 3,
      },
      {
        baseUrl: "http://localhost:5008",
      },
    );

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5008/api/jobs", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        jobType: "PrintMessage",
        payload: '{"message":"hello from frontend"}',
        maxRetries: 3,
      }),
    });
    expect(result).toEqual(responseBody);
  });

  it("throws when submit job fails", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(null, {
        status: 400,
        statusText: "Bad Request",
      }),
    );

    vi.stubGlobal("fetch", fetchMock);

    await expect(
      submitJob({
        jobType: "",
        payload: "",
        maxRetries: -1,
      }),
    ).rejects.toThrow("Failed to submit job. Status: 400");
  });
});
