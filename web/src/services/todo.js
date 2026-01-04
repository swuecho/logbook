import axios from '../axiosConfig.js';

export const fetchTodoContent = async () => {
  const response = await axios.get('/api/todo');
  return response.data;
};
