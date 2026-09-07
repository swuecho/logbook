import axios from '../axiosConfig';
import rawAxios from 'axios';

export const loginUser = async (username: string, password: string) => {
  const response = await axios.post('/api/login', {
    username,
    password,
  });
  return response.data;
};

export const registerUser = async (username: string, password: string) => {
  const response = await axios.post('/api/register', {
    username,
    password,
  });
  return response.data;
};

export const logoutUser = async () => {
  const token = localStorage.getItem('JWT_TOKEN');
  await rawAxios.post('/api/logout', null, { timeout: 3000, headers: { Authorization: `Bearer ${token}` } });
};
