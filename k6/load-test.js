import http from "k6/http";
import { check, sleep, group } from "k6";
import { Counter, Rate, Trend } from "k6/metrics";
import { randomIntBetween } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

const BASE_URL = __ENV.BASE_URL || "http://localhost:5000";

const transactionDuration = new Trend("transaction_duration", true);
const accountCreationDuration = new Trend("account_creation_duration", true);
const failedTransactions = new Counter("failed_transactions");
const successRate = new Rate("success_rate");

export const options = {
  scenarios: {
    smoke: {
      executor: "constant-vus",
      vus: 2,
      duration: "30s",
      startTime: "0s",
      tags: { phase: "smoke" },
    },

    ramp_up: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: "30s", target: 20 },
        { duration: "1m", target: 50 },
        { duration: "30s", target: 100 },
        { duration: "2m", target: 100 },
        { duration: "30s", target: 0 },
      ],
      startTime: "30s",
      tags: { phase: "ramp_up" },
    },

    spike: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: "10s", target: 200 },
        { duration: "30s", target: 200 },
        { duration: "10s", target: 0 },
      ],
      startTime: "5m30s",
      tags: { phase: "spike" },
    },
  },

  thresholds: {
    http_req_duration: ["p(95)<500", "p(99)<1000"],
    http_req_failed: ["rate<0.05"],
    success_rate: ["rate>0.95"],
    transaction_duration: ["p(95)<600"],
    account_creation_duration: ["p(95)<400"],
  },
};

const headers = { "Content-Type": "application/json" };

export function setup() {
  const res = http.post(`${BASE_URL}/api/database/purge`, null, { headers });
  check(res, {
    "database purged (200)": (r) => r.status === 200,
  });
}

function createAccount() {
  const clientId = uuidv4();
  const accountId = uuidv4();
  const payload = JSON.stringify({
    client_id: clientId,
    account_id: accountId,
    initial_balance: randomIntBetween(10000, 1000000),
    credit_limit: randomIntBetween(5000, 500000),
    currency: "BRL",
  });

  const res = http.post(`${BASE_URL}/api/accounts`, payload, { headers });
  accountCreationDuration.add(res.timings.duration);

  const created = check(res, {
    "account created (201)": (r) => r.status === 201,
  });

  successRate.add(created);
  if (!created) failedTransactions.add(1);

  return { clientId, accountId };
}

function getBalance(accountId) {
  const res = http.get(`${BASE_URL}/api/accounts/${accountId}/balance`, {
    headers,
  });

  const ok = check(res, {
    "balance retrieved (200)": (r) => r.status === 200,
  });

  successRate.add(ok);
  return res;
}

function processTransaction(operation, accountId, amount, extra = {}) {
  const payload = JSON.stringify({
    operation,
    account_id: accountId,
    amount,
    currency: "BRL",
    reference_id: uuidv4(),
    ...extra,
  });

  const res = http.post(`${BASE_URL}/api/transactions`, payload, { headers });
  transactionDuration.add(res.timings.duration);

  const ok = check(res, {
    "transaction processed (200)": (r) => r.status === 200,
    "transaction not server error": (r) => r.status < 500,
  });

  successRate.add(ok);
  if (!ok) failedTransactions.add(1);

  return res;
}

function processBatch(transactions) {
  const payload = JSON.stringify(transactions);
  const res = http.post(`${BASE_URL}/api/transactions/batch`, payload, {
    headers,
  });
  transactionDuration.add(res.timings.duration);

  const ok = check(res, {
    "batch processed (200)": (r) => r.status === 200,
    "batch not server error": (r) => r.status < 500,
  });

  successRate.add(ok);
  if (!ok) failedTransactions.add(1);

  return res;
}

export default function () {
  const scenario = randomIntBetween(1, 100);

  if (scenario <= 30) {
    group("credit_debit_flow", () => {
      const { accountId } = createAccount();
      sleep(0.1);

      processTransaction("credit", accountId, randomIntBetween(1000, 50000));
      sleep(0.1);

      processTransaction("debit", accountId, randomIntBetween(100, 5000));
      sleep(0.1);

      getBalance(accountId);
    });
  } else if (scenario <= 55) {
    group("reserve_capture_flow", () => {
      const { accountId } = createAccount();
      sleep(0.1);

      processTransaction("credit", accountId, 100000);
      sleep(0.1);

      const reserveAmount = randomIntBetween(1000, 30000);
      processTransaction("reserve", accountId, reserveAmount);
      sleep(0.2);

      processTransaction("capture", accountId, reserveAmount);
      sleep(0.1);

      getBalance(accountId);
    });
  } else if (scenario <= 75) {
    group("transfer_flow", () => {
      const source = createAccount();
      const dest = createAccount();
      sleep(0.1);

      processTransaction("credit", source.accountId, 200000);
      sleep(0.1);

      processTransaction("transfer", source.accountId, randomIntBetween(1000, 50000), {
        destination_account_id: dest.accountId,
      });
      sleep(0.1);

      getBalance(source.accountId);
      getBalance(dest.accountId);
    });
  } else if (scenario <= 90) {
    group("batch_transactions", () => {
      const { accountId } = createAccount();
      sleep(0.1);

      processTransaction("credit", accountId, 500000);
      sleep(0.1);

      const batch = Array.from({ length: randomIntBetween(3, 8) }, () => ({
        operation: "debit",
        account_id: accountId,
        amount: randomIntBetween(100, 2000),
        currency: "BRL",
        reference_id: uuidv4(),
      }));

      processBatch(batch);
      sleep(0.1);

      getBalance(accountId);
    });
  } else {
    group("reversal_flow", () => {
      const { accountId } = createAccount();
      sleep(0.1);

      const creditRef = uuidv4();
      processTransaction("credit", accountId, 100000);
      sleep(0.1);

      const debitAmount = randomIntBetween(1000, 20000);
      processTransaction("debit", accountId, debitAmount);
      sleep(0.1);

      processTransaction("reversal", accountId, debitAmount);
      sleep(0.1);

      getBalance(accountId);
    });
  }

  sleep(randomIntBetween(1, 3) / 10);
}

export function handleSummary(data) {
  const now = new Date().toISOString().replace(/[:.]/g, "-");
  return {
    stdout: textSummary(data, { indent: " ", enableColors: true }),
    [`/results/summary-${now}.json`]: JSON.stringify(data, null, 2),
  };
}

function textSummary(data, opts) {
  const { metrics } = data;
  const lines = [
    "\n╔══════════════════════════════════════════════════╗",
    "║           PagueVeloz k6 Load Test Results       ║",
    "╠══════════════════════════════════════════════════╣",
    `║  Total Requests:  ${pad(metrics.http_reqs?.values?.count || 0)}  ║`,
    `║  Failed Requests: ${pad(metrics.http_req_failed?.values?.passes || 0)}  ║`,
    `║  Avg Duration:    ${pad(fmt(metrics.http_req_duration?.values?.avg))}  ║`,
    `║  P95 Duration:    ${pad(fmt(metrics.http_req_duration?.values?.["p(95)"]))}  ║`,
    `║  P99 Duration:    ${pad(fmt(metrics.http_req_duration?.values?.["p(99)"]))}  ║`,
    "╚══════════════════════════════════════════════════╝\n",
  ];
  return lines.join("\n");
}

function fmt(val) {
  return val != null ? `${val.toFixed(2)}ms` : "N/A";
}

function pad(val, len = 28) {
  return String(val).padEnd(len);
}
