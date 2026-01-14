export interface CheckResult {
  status: string;
  message?: string;
}

export interface HealthResponse {
  status: string;
  checks: {
    database?: CheckResult;
  };
}
