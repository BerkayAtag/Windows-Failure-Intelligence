// Fixture contains synthetic enrolled devices only; keep it outside source control.
import http from 'k6/http';
import { check, fail } from 'k6';
import exec from 'k6/execution';
import { SharedArray } from 'k6/data';
const count = Number(__ENV.WFI_DEVICE_COUNT || 1000);
if (![1000, 5000, 10000, 40000].includes(count)) throw new Error('Unsupported device count');
const base = __ENV.WFI_BASE_URL || 'http://127.0.0.1:5080';
const address = /^https?:\/\/(\[[0-9a-f:]+\]|[a-z0-9.-]+)(?::[0-9]+)?\/?$/i.exec(base);
if (!address) throw new Error('Supply a server origin without credentials, path, query or fragment');
const host = address[1].toLowerCase();
if (!['127.0.0.1', 'localhost', '[::1]'].includes(host) && host !== __ENV.WFI_AUTHORIZED_TEST_HOST) throw new Error('Only an explicitly authorized test host is allowed');
const agents = new SharedArray('synthetic-agents', () => JSON.parse(open(__ENV.WFI_AGENT_FIXTURE)));
if (agents.length < count || agents.slice(0, count).some(x => !x.synthetic || !x.id || !x.credential)) throw new Error('Supply sufficient synthetic enrolled identities');
export const options = {
  scenarios: { checkins: { executor: 'constant-arrival-rate', rate: Math.ceil(count / 300), timeUnit: '1s', duration: __ENV.WFI_DURATION || '1h', preAllocatedVUs: 20, maxVUs: 200 } },
  thresholds: { http_req_failed: ['rate<0.001'], http_req_duration: ['p(95)<1000'], dropped_iterations: ['count==0'] },
};
export default function () {
  const device = agents[exec.scenario.iterationInTest % count];
  const r = http.post(`${base}/api/v1/agent/me/check-in`, JSON.stringify({ pulse: { agentVersion: 'synthetic-load', osVersion: 'synthetic' }, capabilities: ['combined-check-in-v1'] }),
    { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${device.credential}`, 'X-WFI-Device': device.id }, tags: { name: 'agent-checkin' } });
  check(r, { 'check-in accepted': x => x.status === 200 });
  if (r.status === 200) {
    const body = r.json();
    // This workload deliberately never executes or acknowledges OS mutations.
    if (body.remediation || body.diagnostic) fail('Unexpected work: use an isolated idle fixture with automation disabled');
  }
}
