import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = (__ENV.PERF_BASE_URL || 'http://localhost:8080').replace(/\/$/, '');
const productId = __ENV.PERF_PRODUCT_ID;
const token = __ENV.PERF_ACCESS_TOKEN;
const headers = token ? { Authorization: `Bearer ${token}` } : {};

export const options = {
  scenarios: {
    catalog_reads: {
      executor: 'ramping-vus',
      stages: [
        { duration: '30s', target: 5 },
        { duration: '2m', target: 20 },
        { duration: '30s', target: 0 },
      ],
    },
  },
  tags: { workload: 'guidora-catalog-reads' },
};

export default function () {
  const page = http.get(`${baseUrl}/api/products?page=1&pageSize=20`, { headers });
  check(page, { 'catalog page succeeds': response => response.status === 200 });

  if (productId) {
    const detail = http.get(`${baseUrl}/api/products/${productId}`, { headers });
    const verification = http.get(`${baseUrl}/api/products/${productId}/verification`, { headers });
    const documents = http.get(`${baseUrl}/api/products/${productId}/documents?page=1&pageSize=20`, { headers });
    const changelogs = http.get(`${baseUrl}/api/products/${productId}/changelogs?page=1&pageSize=20`, { headers });
    check(detail, { 'product detail succeeds': response => response.status === 200 });
    check(verification, { 'verification summary succeeds': response => response.status === 200 });
    check(documents, { 'document page succeeds': response => response.status === 200 });
    check(changelogs, { 'changelog page succeeds': response => response.status === 200 });
  }

  sleep(1);
}
