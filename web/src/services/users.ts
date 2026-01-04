import axios from '../axiosConfig';

export const fetchUsersWithDiary = async () => {
  const response = await axios.get('/api/users/with-diary');
  return response.data;
};

export const deleteUser = async (userId: string) => {
  await axios.delete(`/api/users/${userId}`);
};
