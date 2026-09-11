import axios from 'axios';
import { syncCredentials } from './services/session';

const instance = axios.create({ timeout: 12000 });
instance.interceptors.request.use(config => {
  const credentials = syncCredentials();
  if (!credentials) throw new Error('Sign in to continue.');
  const url = new URL(config.url || '', config.baseURL || location.origin);
  if (url.origin !== location.origin) throw new Error('Only same-origin API requests are allowed.');
  config.headers['X-CSRF-Token'] = credentials.csrfToken;
  return config;
});
export default instance;
