import axios from '../axiosConfig.js';

export const fetchUsersWithDiary = async () => {
  const response = await axios.get('/api/users/with-diary');
  return response.data;
};

export const deleteUser = async (userId) => {
  await axios.delete(`/api/users/${userId}`);
};
