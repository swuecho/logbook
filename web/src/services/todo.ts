import axios from '../axiosConfig';

export const fetchTodoContent = async () => {
  const response = await axios.get('/api/todo');
  return response.data;
};
