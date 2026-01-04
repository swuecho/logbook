import axios from '../axiosConfig.js';

export const loginUser = async (username, password) => {
  const response = await axios.post('/api/login', {
    Username: username,
    Password: password,
  });
  return response.data;
};

export const logoutUser = async () => {
  await axios.post('/api/logout');
};
