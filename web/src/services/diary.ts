import axios from '../axiosConfig';


export const getDiaryIds = async (): Promise<string[]> => {
        const response = await axios.get('/api/diary_ids');
        return response.data;
};

export const getDiarySummaries = async () => {
        const response = await axios.get('/api/diary');
        return response.data;
};
