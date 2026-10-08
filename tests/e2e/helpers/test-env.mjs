const BASE_URL = process.env.R2WAI_BASE_URL ?? 'http://localhost:3001';
const API_URL = process.env.R2WAI_API_URL ?? 'http://localhost:5000';
const EMAIL = process.env.R2WAI_TEST_EMAIL;
const PASSWORD = process.env.R2WAI_TEST_PASSWORD;

if (!EMAIL || !PASSWORD) {
  console.error(
    'Missing credentials. Set R2WAI_TEST_EMAIL and R2WAI_TEST_PASSWORD before running this script.'
  );
  process.exit(1);
}

export { BASE_URL, API_URL, EMAIL, PASSWORD };
