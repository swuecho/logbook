import axios from '../axiosConfig.js';


export const getDiaryIds = async (): Promise<string[]> => {
        const response = await axios.get('/api/diray_ids');
        return response.data;
}; 