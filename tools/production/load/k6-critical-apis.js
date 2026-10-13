import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  scenarios: {
    read_mix: {
      executor: 'constant-vus',
      vus: Number(__ENV.VUS || 5),
      duration: __ENV.DURATION || '30s',
      exec: 'readMix',
    },
    auth_burst_contract: {
      executor: 'shared-iterations',
      vus: Number(__ENV.AUTH_VUS || 2),
      iterations: Number(__ENV.AUTH_ITERATIONS || 2),
      exec: 'authBurst',
      startTime: '1s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

const baseUrl = (__ENV.BASE_URL || '').replace(/\/$/, '');
const token = __ENV.ACCESS_TOKEN || '';
const adminToken = __ENV.ADMIN_ACCESS_TOKEN || '';
const conversationId = __ENV.CONVERSATION_ID || '';
const classId = __ENV.CLASS_ID || '';

if (!baseUrl || __ENV.CONFIRM_ISOLATED_STAGING !== 'YES') {
  throw new Error('BASE_URL and CONFIRM_ISOLATED_STAGING=YES are required. Never target Production.');
}

function bearer(value) {
  return { headers: { Authorization: `Bearer ${value}` } };
}

function measuredGet(name, path, accessToken = token) {
  const response = http.get(`${baseUrl}${path}`, bearer(accessToken));
  check(response, { [`${name}: status is not 5xx`]: (r) => r.status < 500 });
}

function testUuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (value) => {
    const random = Math.floor(Math.random() * 16);
    const hex = value === 'x' ? random : ((random & 0x3) | 0x8);
    return hex.toString(16);
  });
}

export function readMix() {
  measuredGet('conversation list', '/api/v1/conversations');
  measuredGet('contacts', '/api/v1/contacts?page=1&pageSize=30');
  measuredGet('student dashboard', '/api/v1/dashboards/student');
  measuredGet('student reports', '/api/v1/reports/student/assignments?page=1&pageSize=30');
  measuredGet('manager commerce summary', '/api/v1/commerce/summary');
  if (conversationId) {
    measuredGet('message history', `/api/v1/conversations/${conversationId}/messages?limit=50`);
  }
  if (classId) {
    measuredGet('class report', `/api/v1/reports/manager/classes/${classId}`);
  }
  if (adminToken) {
    measuredGet('backoffice organizations', '/api/admin/v1/organizations?page=1&pageSize=30', adminToken);
  }
  sleep(1);
}

export function authBurst() {
  if (__ENV.ENABLE_OTP_BURST !== 'YES' || !__ENV.TEST_PHONE) {
    return;
  }

  const response = http.post(
    `${baseUrl}/api/v1/auth/otp/challenges`,
    JSON.stringify({ phoneNumber: __ENV.TEST_PHONE }),
    { headers: { 'Content-Type': 'application/json' } });
  check(response, { 'OTP request is accepted or limited': (r) => [202, 429].includes(r.status) });
}

export function sendMessage() {
  if (__ENV.ENABLE_MESSAGE_SEND !== 'YES' || !conversationId) {
    throw new Error('ENABLE_MESSAGE_SEND=YES and CONVERSATION_ID are required.');
  }

  const response = http.post(
    `${baseUrl}/api/v1/conversations/${conversationId}/messages`,
    JSON.stringify({
      clientMessageId: testUuid(),
      kind: 'Text',
      text: 'staging load probe',
      attachmentIds: [],
    }),
    { headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' } });
  check(response, { 'send message succeeds without 5xx': (r) => r.status < 500 });
}
