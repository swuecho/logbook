import axios from '../axiosConfig';

export const loginUser = async (username: string, password: string) => {
  const response = await axios.post('/api/login', {
    Username: username,
    Password: password,
  });
  return response.data;
};

export const logoutUser = async () => {
  await axios.post('/api/logout');
};
