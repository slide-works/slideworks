// Every line below breaks a rule of eslint.config.js on purpose. src/core.test.ts lints this file as if it sat in src/.
import { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { View } from 'react-native';
import { api } from '../SlideWorks.Client/src/api/client';

export const used = [useState, createRoot, View, api];
export const width = window.innerWidth;
export const title = document.title;
export const saved = localStorage.getItem('k');
export const tab = sessionStorage.getItem('k');
export const online = navigator.onLine;
export const globalThisWindow = globalThis.window;
export const globalThisDocument = globalThis.document;
export const globalThisLocalStorage = globalThis.localStorage;
export const globalThisSessionStorage = globalThis.sessionStorage;
export const globalThisNavigator = globalThis.navigator;
export const selfWindow = self.window;
export const selfDocument = self.document;
export const selfLocalStorage = self.localStorage;
export const selfSessionStorage = self.sessionStorage;
export const selfNavigator = self.navigator;
