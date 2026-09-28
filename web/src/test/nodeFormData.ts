/// <reference types="node" />
import { Blob as NodeBlob, File as NodeFile } from 'node:buffer';

// jsdom's FormData, File and Blob cannot be serialised by Node's fetch, so multipart uploads would never reach MSW.
// Imported first by setup.ts so every module that captures File (zod schemas) sees the Node classes.
const NodeFormData = (await new Response(new URLSearchParams()).formData()).constructor;

Object.assign(globalThis, { FormData: NodeFormData, File: NodeFile, Blob: NodeBlob });
