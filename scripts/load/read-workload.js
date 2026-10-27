import http from 'k6/http';
import { check, fail } from 'k6';

const base = __ENV.WFI_BASE_URL;
const cookie = __ENV.WFI_SESSION;
if (!base || !base.startsWith('https://') || !cookie) throw new Error('HTTPS WFI_BASE_URL and test WFI_SESSION are required.');
export const options = {
  scenarios: { reads: { executor: 'ramping-arrival-rate', startRate: 1, timeUnit: '1s', preAllocatedVUs: 20, maxVUs: 100,
    stages: [{ target: 10, duration: '1m' }, { target: 50, duration: '3m' }, { target: 100, duration: '3m' }, { target: 0, duration: '1m' }] } },
  thresholds: { http_req_failed: ['rate<0.001'], http_req_duration: ['p(95)<1000'], checks: ['rate>0.999'], dropped_iterations: ['count==0'] },
};
export function setup() {
  const response = http.get(base + '/api/v1/auth/me', { headers: { Cookie: 'WFI.Session=' + cookie } });
  if (response.status !== 200) fail('Test session unavailable; no authenticated workload started.');
}
export default function () {
  const response = http.get(base + '/api/v1/analyses?page=1', { headers: { Cookie: 'WFI.Session=' + cookie } });
  check(response, { 'authorized paged response': r => r.status === 200 && Array.isArray(r.json('items')) && r.json('items').length <= 25 });
}
