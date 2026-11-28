// Metro and Jest (jest-expo) transform through Babel; Vitest does not use this file.
module.exports = function (api) {
  api.cache(true);
  return { presets: ['babel-preset-expo'] };
};
