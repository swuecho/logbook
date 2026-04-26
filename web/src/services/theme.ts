import { ref } from 'vue';

const THEME_STORAGE_KEY = 'LOGBOOK_THEME';
const DEFAULT_THEME = 'default';
const VINE_THEME = 'vine';
const DAISY_THEME = 'daisy';
const THEMES = [DEFAULT_THEME, VINE_THEME, DAISY_THEME] as const;

type ThemeName = (typeof THEMES)[number];

const currentTheme = ref<ThemeName>(DEFAULT_THEME);
const themeLabels: Record<ThemeName, string> = {
  default: 'Default',
  vine: 'Vine',
  daisy: 'Daisy',
};
const themeOptions = THEMES.map((value) => ({
  value,
  label: themeLabels[value],
}));

const normalizeTheme = (theme: string | null): ThemeName => {
  if (theme === 'photo') return VINE_THEME;
  return THEMES.includes(theme as ThemeName) ? theme as ThemeName : DEFAULT_THEME;
};

const applyTheme = (theme: ThemeName) => {
  currentTheme.value = theme;

  if (typeof document === 'undefined') return;

  if (theme === DEFAULT_THEME) {
    delete document.documentElement.dataset.theme;
  } else {
    document.documentElement.dataset.theme = theme;
  }
};

const setTheme = (theme: ThemeName) => {
  const nextTheme = normalizeTheme(theme);
  applyTheme(nextTheme);

  if (typeof localStorage === 'undefined') return;

  if (nextTheme === DEFAULT_THEME) {
    localStorage.removeItem(THEME_STORAGE_KEY);
  } else {
    localStorage.setItem(THEME_STORAGE_KEY, nextTheme);
  }
};

const initTheme = () => {
  if (typeof localStorage === 'undefined') {
    applyTheme(DEFAULT_THEME);
    return;
  }

  applyTheme(normalizeTheme(localStorage.getItem(THEME_STORAGE_KEY)));
};

const getNextTheme = (theme: ThemeName) => {
  const currentIndex = THEMES.indexOf(theme);
  return THEMES[(currentIndex + 1) % THEMES.length];
};

const toggleTheme = () => {
  setTheme(getNextTheme(currentTheme.value));
};

export { currentTheme, getNextTheme, initTheme, setTheme, themeLabels, themeOptions, toggleTheme };
