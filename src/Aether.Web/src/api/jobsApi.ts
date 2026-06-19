export const JobStatus = {
  Pending: 1,
  Processing: 2,
  Completed: 3,
  Failed: 4,
  Cancelled: 5,
} as const;

export type JobStatus = (typeof JobStatus)[keyof typeof JobStatus];

export type JobDto = {
  id: string;
  jobType: string;
  status: JobStatus;
  retryCount: number;
  maxRetries: number;
  errorMessage: string | null;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  nextRetryAtUtc: string | null;
};

export type SubmitJobRequestDto = {
  jobType: string;
  payload: string;
  maxRetries: number;
};

export type SubmitJobResponseDto = {
  id: string;
  jobType: string;
  status: JobStatus;
  retryCount: number;
  maxRetries: number;
  createdAtUtc: string;
};

export type GetJobsOptions = {
  baseUrl?: string;
  status?: JobStatus;
  page?: number;
  pageSize?: number;
};

export type GetJobByIdOptions = {
  baseUrl?: string;
};

export type SubmitJobOptions = {
  baseUrl?: string;
};

export async function getJobs(options: GetJobsOptions = {}): Promise<JobDto[]> {
  const url = buildJobsUrl(options);

  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(`Failed to fetch jobs. Status: ${response.status}`);
  }

  return response.json() as Promise<JobDto[]>;
}

export async function getJobById(
  jobId: string,
  options: GetJobByIdOptions = {},
): Promise<JobDto> {
  const url = buildJobByIdUrl(jobId, options);

  const response = await fetch(url);

  if (response.status === 404) {
    throw new Error(`Job '${jobId}' was not found.`);
  }

  if (!response.ok) {
    throw new Error(`Failed to fetch job. Status: ${response.status}`);
  }

  return response.json() as Promise<JobDto>;
}

export async function submitJob(
  request: SubmitJobRequestDto,
  options: SubmitJobOptions = {},
): Promise<SubmitJobResponseDto> {
  const url = buildSubmitJobUrl(options);

  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error(`Failed to submit job. Status: ${response.status}`);
  }

  return response.json() as Promise<SubmitJobResponseDto>;
}

function buildJobsUrl(options: GetJobsOptions): string {
  const baseUrl = trimTrailingSlash(options.baseUrl ?? "");
  const query = new URLSearchParams();

  if (options.status !== undefined) {
    query.set("status", String(options.status));
  }

  if (options.page !== undefined) {
    query.set("page", String(options.page));
  }

  if (options.pageSize !== undefined) {
    query.set("pageSize", String(options.pageSize));
  }

  const queryString = query.toString();

  if (queryString.length === 0) {
    return `${baseUrl}/api/jobs`;
  }

  return `${baseUrl}/api/jobs?${queryString}`;
}

function buildJobByIdUrl(jobId: string, options: GetJobByIdOptions): string {
  const baseUrl = trimTrailingSlash(options.baseUrl ?? "");
  const encodedJobId = encodeURIComponent(jobId);

  return `${baseUrl}/api/jobs/${encodedJobId}`;
}

function buildSubmitJobUrl(options: SubmitJobOptions): string {
  const baseUrl = trimTrailingSlash(options.baseUrl ?? "");

  return `${baseUrl}/api/jobs`;
}

function trimTrailingSlash(value: string): string {
  return value.endsWith("/") ? value.slice(0, -1) : value;
}
