import axios from 'axios';
import { syncCredentials, requireSignIn, handleSessionFailure } from './services/session';

const instance = axios.create({ timeout: 12000 });
instance.interceptors.request.use(config => {
  const credentials = syncCredentials();
  if (!credentials) {
    requireSignIn();
    throw new Error('Sign in to continue.');
  }
  const url = new URL(config.url || '', config.baseURL || location.origin);
  if (url.origin !== location.origin) throw new Error('Only same-origin API requests are allowed.');
  config.headers['X-CSRF-Token'] = credentials.csrfToken;
  return config;
});
instance.interceptors.response.use(response => response, error => {
  const token = error.config?.headers?.['X-CSRF-Token'];
  if (token) handleSessionFailure(error.response?.status, error.response?.data?.code, token);
  return Promise.reject(error);
});
export default instance;
